using System.Globalization;
using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Application.Abstractions;
using WhereToEat.Recommendation.Infrastructure.Medians;
using WhereToEat.SharedKernel.Geo;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Infrastructure.Candidates;

/// <summary>
/// The Dapper <see cref="IRecommendationCandidateSource"/>: it runs the candidate query
/// (<see cref="RecommendationSql.SelectCandidates"/>) which joins the catalog menu read model, the
/// geography radius pre-filter, and the materialized rating aggregate by id, then groups the rows
/// into per-restaurant candidates. The smoothed rating is computed <b>in code</b> from the joined
/// sum/count via <see cref="SmoothedRatingMath"/> — a DB join carries the inputs, and <b>no</b>
/// <c>Ratings.Domain</c> type is referenced (the cross-module boundary the Step 2 fitness test
/// guards). Exact distance is decided in code via <see cref="Haversine"/> (the spatial index
/// narrows, Haversine decides — invariant #7).
/// </summary>
public sealed class DapperRecommendationCandidateSource : IRecommendationCandidateSource
{
    private const double MetersPerKm = 1000d;

    // An impossible id passed in place of an empty selection list so Dapper's "IN @ids" expansion
    // produces a valid (always-empty) IN clause rather than invalid SQL.
    private static readonly Guid[] NoIds = [Guid.Empty];

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly RatingSmoothingSettings _smoothingSettings;

    public DapperRecommendationCandidateSource(
        ISqlConnectionFactory connectionFactory,
        RatingSmoothingSettings smoothingSettings)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(smoothingSettings);
        _connectionFactory = connectionFactory;
        _smoothingSettings = smoothingSettings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecommendationCandidate>> LoadCandidatesAsync(
        IReadOnlyList<SelectedItem> items,
        UserGeo? userGeo,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return Array.Empty<RecommendationCandidate>();
        }

        var dishIds = items.Where(i => i.DishId is not null).Select(i => i.DishId!.Value).ToArray();
        var categoryIds = items.Where(i => i.CategoryId is not null).Select(i => i.CategoryId!.Value).ToArray();

        string? wkt = null;
        GeoPoint? origin = null;
        if (userGeo is not null)
        {
            var originResult = GeoPoint.Create(userGeo.Latitude, userGeo.Longitude);
            if (originResult.IsSuccess)
            {
                origin = originResult.Value;
                // WKT is longitude-first; invariant culture keeps the decimal separator a dot.
                wkt = string.Format(
                    CultureInfo.InvariantCulture,
                    "POINT({0} {1})",
                    userGeo.Longitude,
                    userGeo.Latitude);
            }
        }

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<CandidateRow>(
            new CommandDefinition(
                RecommendationSql.SelectCandidates,
                new
                {
                    DishIds = dishIds.Length > 0 ? dishIds : NoIds,
                    CategoryIds = categoryIds.Length > 0 ? categoryIds : NoIds,
                    Wkt = wkt,
                    RadiusMeters = radiusKm * MetersPerKm,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return GroupIntoCandidates(rows, origin);
    }

    private List<RecommendationCandidate> GroupIntoCandidates(
        IEnumerable<CandidateRow> rows,
        GeoPoint? origin)
    {
        var candidates = new List<RecommendationCandidate>();

        foreach (var group in rows.GroupBy(r => r.RestaurantId))
        {
            var first = group.First();

            var matched = group
                .Select(r => new MatchedItemDto(r.CategoryId, r.DishId, r.PriceAmount, r.PriceCurrency))
                .ToList();

            // The smoothed rating is derived from the joined aggregate inputs here, by arithmetic —
            // NOT by a Ratings.Domain call. This is the only place the rating value is produced for
            // the engine, and it crosses the boundary purely as this DTO field.
            var smoothed = SmoothedRatingMath.Smooth(first.ScoreSum, first.ScoreCount, _smoothingSettings);
            var ratingCount = (int)Math.Min(first.ScoreCount ?? 0L, int.MaxValue);

            double? distanceKm = null;
            if (origin is not null && first.Latitude is not null && first.Longitude is not null)
            {
                var pointResult = GeoPoint.Create(first.Latitude.Value, first.Longitude.Value);
                if (pointResult.IsSuccess)
                {
                    // The spatial index narrowed the set; Haversine decides the exact distance (invariant #7).
                    distanceKm = Haversine.DistanceKm(origin, pointResult.Value);
                }
            }

            candidates.Add(new RecommendationCandidate(
                first.RestaurantId,
                first.Name,
                matched,
                smoothed,
                ratingCount,
                distanceKm));
        }

        return candidates;
    }
}
