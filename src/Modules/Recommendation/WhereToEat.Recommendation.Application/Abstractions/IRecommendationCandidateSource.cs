using WhereToEat.Contracts.Recommendation;

namespace WhereToEat.Recommendation.Application.Abstractions;

/// <summary>
/// The port that loads recommendation candidates from the read side: given the user's selection,
/// their optional location, and the search radius, it returns the venues that have at least one of
/// the selected items, each with its matched offerings, the venue's <b>precomputed</b> smoothed
/// rating, and the exact Haversine distance from the user. The infrastructure adapter joins the
/// catalog read model, the radius pre-filter, <b>and the materialized rating aggregate</b> — the
/// rating crosses the module boundary <b>only as the DTO field</b>
/// <see cref="RecommendationCandidate.SmoothedRating"/>, via a DB join, never a code reference into
/// <c>Ratings.Domain</c> (the boundary the Step 2 fitness test guards).
/// </summary>
public interface IRecommendationCandidateSource
{
    /// <summary>
    /// Loads the candidate venues for <paramref name="items"/>. When <paramref name="userGeo"/> is
    /// supplied, candidates are narrowed to <paramref name="radiusKm"/> and the distance is computed
    /// locally (Haversine, invariant #7); when it is <c>null</c>, distance is not applied and the
    /// returned distance is <c>null</c>. Returns an empty list when nothing matches.
    /// </summary>
    Task<IReadOnlyList<RecommendationCandidate>> LoadCandidatesAsync(
        IReadOnlyList<SelectedItem> items,
        UserGeo? userGeo,
        double radiusKm,
        CancellationToken cancellationToken = default);
}
