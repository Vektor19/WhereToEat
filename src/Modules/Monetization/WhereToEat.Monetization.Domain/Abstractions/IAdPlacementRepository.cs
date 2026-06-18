using WhereToEat.Monetization.Domain.Identifiers;

namespace WhereToEat.Monetization.Domain.Abstractions;

/// <summary>
/// Write port for <see cref="AdPlacement"/> — the always-labeled, separate ad slots (invariant #10).
/// A Domain-shaped contract; the Dapper adapter lives in <c>Monetization.Infrastructure</c>. Note
/// there is deliberately no method here that the recommendation pipeline could call: ad placements are
/// a marked slot, never an organic-ranking input — the Recommendation module never references this
/// module at all (a Step 14 guard test pins that).
/// </summary>
public interface IAdPlacementRepository
{
    /// <summary>Persists a newly created labeled ad placement.</summary>
    Task AddAsync(AdPlacement placement, CancellationToken cancellationToken = default);

    /// <summary>Loads a placement by id, or <c>null</c> when none exists.</summary>
    Task<AdPlacement?> GetByIdAsync(AdPlacementId id, CancellationToken cancellationToken = default);
}
