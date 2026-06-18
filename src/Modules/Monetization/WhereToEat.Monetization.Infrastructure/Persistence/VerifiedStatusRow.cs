namespace WhereToEat.Monetization.Infrastructure.Persistence;

/// <summary>
/// The flat row shape Dapper materializes from <c>monetization.VerifiedStatus</c> before
/// <see cref="DapperVerifiedStatusRepository"/> rehydrates it into the domain aggregate. Kept internal
/// — it is a persistence detail, not part of the module's public surface.
/// </summary>
internal sealed class VerifiedStatusRow
{
    public Guid VenueId { get; init; }

    /// <summary>The <c>SubscriptionTier</c> enum value (0 = None, 1 = Basic, 2 = Pro).</summary>
    public int Tier { get; init; }
}
