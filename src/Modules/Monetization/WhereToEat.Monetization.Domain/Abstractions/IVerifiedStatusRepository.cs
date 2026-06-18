using WhereToEat.Monetization.Domain.Identifiers;

namespace WhereToEat.Monetization.Domain.Abstractions;

/// <summary>
/// Read/write port for the per-venue <see cref="VerifiedStatus"/> aggregate (the grant/revoke
/// use-cases persist through this). A Domain-shaped contract — no SQL leaks; the Dapper adapter lives
/// in <c>Monetization.Infrastructure</c>. The status it stores gates the Step 5 real-photo permission
/// (via <see cref="VerifiedStatus.CanManageRealPhotos"/>); it never touches organic ranking
/// (invariant #10) or the always-free contact links (§5.8).
/// </summary>
public interface IVerifiedStatusRepository
{
    /// <summary>
    /// Loads the venue's Verified status, or <c>null</c> when the venue has none yet (the freemium
    /// baseline — still fully in the catalog for free).
    /// </summary>
    Task<VerifiedStatus?> GetByVenueAsync(VenueRef venue, CancellationToken cancellationToken = default);

    /// <summary>Inserts or overwrites the venue's Verified status (idempotent upsert).</summary>
    Task UpsertAsync(VerifiedStatus status, CancellationToken cancellationToken = default);
}
