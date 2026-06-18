using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Application.Abstractions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Filtering;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Application;

/// <summary>
/// The <c>POST /recommend</c> use-case handler. It orchestrates the 5-step pipeline from CLAUDE.md
/// §6 without owning any of the ranking logic:
/// <list type="number">
///   <item>validates the request and resolves the match/sort strategies and the selected filters
///   <b>by key</b> (invariant #4 — unknown key ⇒ a clean validation failure);</item>
///   <item>loads candidates through <see cref="IRecommendationCandidateSource"/> (the smoothed
///   rating rides in as a DTO field — no <c>Ratings.Domain</c> reference);</item>
///   <item>reads the precomputed market median through <see cref="IPriceMedianProvider"/> (DB-only;
///   never per request) and builds the <see cref="ScoringContext"/>;</item>
///   <item>runs the pure-domain <see cref="RecommendationPipeline"/> and projects the ordered,
///   filtered result to the public <see cref="RecommendationResultDto"/>.</item>
/// </list>
/// </summary>
public sealed class RecommendQuery
{
    /// <summary>The search radius (km) when the user supplies a location. A sensible city default.</summary>
    public const double DefaultRadiusKm = 15d;

    private readonly IRecommendationCandidateSource _candidateSource;
    private readonly IPriceMedianProvider _medianProvider;
    private readonly IStrategyResolver _strategyResolver;
    private readonly RecommendationPipeline _pipeline;

    public RecommendQuery(
        IRecommendationCandidateSource candidateSource,
        IPriceMedianProvider medianProvider,
        IStrategyResolver strategyResolver,
        RecommendationPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(candidateSource);
        ArgumentNullException.ThrowIfNull(medianProvider);
        ArgumentNullException.ThrowIfNull(strategyResolver);
        ArgumentNullException.ThrowIfNull(pipeline);

        _candidateSource = candidateSource;
        _medianProvider = medianProvider;
        _strategyResolver = strategyResolver;
        _pipeline = pipeline;
    }

    /// <summary>
    /// Runs the recommendation for <paramref name="request"/>. Returns the ordered result, or a
    /// validation failure for an empty selection or an unknown match/sort/filter key.
    /// </summary>
    public async Task<Result<RecommendationResultDto>> ExecuteAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items is null || request.Items.Count == 0)
        {
            return Result.Failure<RecommendationResultDto>(
                Error.Validation("Recommend.NoItems", "At least one category or dish must be selected."));
        }

        var match = _strategyResolver.ResolveMatch(request.Match);
        if (match is null)
        {
            return Result.Failure<RecommendationResultDto>(
                Error.Validation("Recommend.UnknownMatch", $"Unknown match mode '{request.Match}'."));
        }

        var sort = _strategyResolver.ResolveSort(request.Sort);
        if (sort is null)
        {
            return Result.Failure<RecommendationResultDto>(
                Error.Validation("Recommend.UnknownSort", $"Unknown sort mode '{request.Sort}'."));
        }

        var filters = new List<(IResultFilter Filter, string? Value)>();
        foreach (var selection in request.Filters ?? Array.Empty<FilterSelection>())
        {
            var filter = _strategyResolver.ResolveFilter(selection.Key);
            if (filter is null)
            {
                return Result.Failure<RecommendationResultDto>(
                    Error.Validation("Recommend.UnknownFilter", $"Unknown filter '{selection.Key}'."));
            }

            filters.Add((filter, selection.Value));
        }

        var distanceEnabled = request.UserGeo is not null;

        var candidates = await _candidateSource
            .LoadCandidatesAsync(request.Items, request.UserGeo, DefaultRadiusKm, cancellationToken)
            .ConfigureAwait(false);

        var median = await _medianProvider
            .GetMedianBasketAmountAsync(request.Items, request.UserGeo, cancellationToken)
            .ConfigureAwait(false);

        var options = _strategyResolver.OptionsFor(request.Sort);
        var context = new ScoringContext(options, median, request.Items.Count, distanceEnabled);

        var domainCandidates = candidates.Select(ToDomain).ToList();

        var ranked = _pipeline.Run(domainCandidates, request.Items.Count, match, sort, filters, context);

        var restaurants = ranked
            .Select(c => new RecommendedRestaurantDto(
                c.RestaurantId,
                c.Name,
                c.BasketPrice.Amount,
                c.BasketPrice.Currency,
                c.SmoothedRating,
                c.RatingCount,
                c.DistanceKm,
                c.Coverage))
            .ToList();

        return Result.Success(new RecommendationResultDto(request.Match, request.Sort, restaurants));
    }

    private static RestaurantCandidate ToDomain(RecommendationCandidate candidate)
    {
        var matched = candidate.MatchedItems
            .Select(item =>
            {
                var selection = item.DishId is { } dishId
                    ? SelectionKey.Dish(dishId)
                    : SelectionKey.Category(item.CategoryId
                        ?? throw new InvalidOperationException("A matched item must carry a category or dish id."));

                var priceResult = Money.Create(item.PriceAmount, item.PriceCurrency);
                if (priceResult.IsFailure)
                {
                    // A stored price that violates the Money invariants is a data-integrity bug, not
                    // recoverable input — surface it rather than ranking a silently-wrong basket.
                    throw new InvalidOperationException(
                        $"Candidate '{candidate.RestaurantId}' has an invalid matched-item price: {priceResult.Error.Message}");
                }

                return new MatchedItem(selection, priceResult.Value);
            })
            .ToList();

        return new RestaurantCandidate(
            candidate.RestaurantId,
            candidate.Name,
            matched,
            candidate.SmoothedRating,
            candidate.RatingCount,
            candidate.DistanceKm);
    }
}
