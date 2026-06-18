namespace WhereToEat.Monetization.Application.Verified;

/// <summary>
/// Revokes a venue's Verified status back to the freemium baseline. The venue stays fully in the
/// catalog for free; the real-photo permission gate closes (Step 5) and the tier returns to
/// <c>None</c>. As with granting, organic ranking and the always-free contact links are untouched
/// (invariant #10 / §5.8). Revoking takes no payment (the seam is grant-only).
/// </summary>
/// <param name="VenueId">The venue (its restaurant Guid) whose Verified status is revoked.</param>
public sealed record RevokeVerifiedCommand(Guid VenueId);
