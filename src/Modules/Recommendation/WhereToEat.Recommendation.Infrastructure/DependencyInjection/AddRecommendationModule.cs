using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Recommendation.Application;
using WhereToEat.Recommendation.Application.Abstractions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Filtering;
using WhereToEat.Recommendation.Domain.Matching;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;
using WhereToEat.Recommendation.Infrastructure.Candidates;
using WhereToEat.Recommendation.Infrastructure.Medians;

namespace WhereToEat.Recommendation.Infrastructure.DependencyInjection;

/// <summary>
/// The per-module composition entry point the public host calls (the modular-monolith convention):
/// it wires the pluggable match/sort/filter strategies <b>by key</b> (invariant #4), the four
/// normalized scoring functions, the per-mode weights, the DB-only median provider, the Dapper
/// candidate source, the pipeline, and the <see cref="RecommendQuery"/> handler. A new
/// match/sort/filter is added by registering it in the relevant collection here — the resolver,
/// pipeline, and handler do not change. The median provider is <b>DB-only (no Redis)</b>; Step 13
/// later decorates the same <see cref="IPriceMedianProvider"/> registration transparently.
/// </summary>
public static class AddRecommendationModuleExtensions
{
    /// <summary>
    /// Registers the recommendation module against <paramref name="connectionString"/>, with optional
    /// rating-smoothing settings (defaults applied when omitted).
    /// </summary>
    public static IServiceCollection AddRecommendationModule(
        this IServiceCollection services,
        string connectionString,
        RatingSmoothingSettings? smoothingSettings = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        // The connection factory is registered by the catalog module too; TryAdd keeps a single
        // factory if the host already wired one, otherwise registers ours.
        services.TryAddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        services.AddSingleton(smoothingSettings ?? new RatingSmoothingSettings());

        // --- Step 1 match predicates (keyed; new ones self-register by being added here) ----------
        services.AddSingleton<IMatchStrategy, OrMatchStrategy>();
        services.AddSingleton<IMatchStrategy, AndMatchStrategy>();

        // --- The four normalized scoring functions (each an independent pluggable module) ---------
        services.AddSingleton<IScoringFunction, FPrice>();
        services.AddSingleton<IScoringFunction, FQuality>();
        services.AddSingleton<IScoringFunction, FDistance>();
        services.AddSingleton<IScoringFunction, FCoverage>();

        // --- Step 3 sort strategies -------------------------------------------------------------
        // Explicit sorts: the chosen field dominates, coverage is only a tie-breaker (invariant #5).
        services.AddSingleton<ISortStrategy>(ExplicitSortStrategy.Price());
        services.AddSingleton<ISortStrategy>(ExplicitSortStrategy.Distance());
        services.AddSingleton<ISortStrategy>(ExplicitSortStrategy.Rating());

        // Composite modes: weighted blend of the registered scoring functions, sorted by score.
        services.AddSingleton<ISortStrategy>(sp => new CompositeScoreStrategy(
            CompositeScoreStrategy.PriceQualityKey,
            sp.GetServices<IScoringFunction>().ToList(),
            RecommendationModes.PriceQualityWeights));
        services.AddSingleton<ISortStrategy>(sp => new CompositeScoreStrategy(
            CompositeScoreStrategy.BestKey,
            sp.GetServices<IScoringFunction>().ToList(),
            RecommendationModes.BestWeights));

        // --- Step 4 filters (keyed; composable; seam for open-now/vegan/delivery) -----------------
        services.AddSingleton<IResultFilter, MaxPriceFilter>();
        services.AddSingleton<IResultFilter, MinRatingFilter>();

        // --- The resolver + pipeline + ports + handler -------------------------------------------
        services.AddSingleton<IStrategyResolver>(sp => new KeyedStrategyResolver(
            sp.GetServices<IMatchStrategy>(),
            sp.GetServices<ISortStrategy>(),
            sp.GetServices<IResultFilter>(),
            RecommendationModes.OptionsByKey,
            RecommendationModes.DefaultOptions));

        services.AddSingleton<RecommendationPipeline>();

        // The DB-only median provider (Step 7) is always registered as the concrete type. The
        // IPriceMedianProvider port resolves to the Step 13 caching decorator WHEN an ICacheService is
        // present (a host that called AddRedisCache), otherwise straight to the DB provider — so the
        // engine's results are identical with caching on or off (the decorator is transparent).
        services.AddScoped<DbPriceMedianProvider>();
        services.AddScoped<IPriceMedianProvider>(sp =>
        {
            var dbProvider = sp.GetRequiredService<DbPriceMedianProvider>();
            var cache = sp.GetService<ICacheService>();
            return cache is null or NullCacheService
                ? dbProvider
                : new CachingPriceMedianProvider(dbProvider, cache);
        });
        services.AddScoped<IRecommendationCandidateSource, DapperRecommendationCandidateSource>();
        services.AddScoped<RecommendQuery>();

        // The write half of the median feature (Step 12 nightly job): recomputes + persists the same
        // DB-only table the provider above reads. Registered here so the worker host gets it by calling
        // AddRecommendationModule; the read path stays DB-only (Redis is a Step 13 decorator over the
        // provider, never over this writer).
        services.AddScoped<IPriceMedianWriter, PriceMedianWriter>();

        return services;
    }
}
