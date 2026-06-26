namespace WhereToEat.Ratings.Application.Submit;

/// <summary>
/// A registered user submitting (or revising) their score for a restaurant (invariant #6 — our own,
/// cumulative all-time ratings). The caller's identity has already been resolved to the opaque
/// <paramref name="UserId"/> Guid by the authenticated endpoint (Step 22): no PII reaches this layer
/// (invariant #11). The score is validated against the domain's 1..5 scale by the handler.
/// </summary>
/// <param name="RestaurantId">The restaurant the score is about (the same Guid Catalog's RestaurantId carries).</param>
/// <param name="UserId">The opaque user reference (the IdP <c>sub</c> mapped to the user's internal Guid).</param>
/// <param name="Score">The score on the 1..5 scale.</param>
public sealed record SubmitRatingCommand(Guid RestaurantId, Guid UserId, int Score);
