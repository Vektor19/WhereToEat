namespace WhereToEat.Monetization.Application.Ads;

/// <summary>
/// Creates an <b>always-labeled</b> ad placement for a venue (§7.2 / invariant #10). The placement is a
/// separate, marked slot that the recommendation pipeline (Step 7) <b>never</b> uses as a rank
/// modifier — organic ranking is not for sale. <paramref name="TargetingKey"/> selects <i>where</i> the
/// labeled slot is shown (e.g. a dish-category + geo), not how organic results rank.
/// </summary>
/// <param name="VenueId">The venue (its restaurant Guid) the labeled slot advertises.</param>
/// <param name="TargetingKey">The audience-targeting key (e.g. "pizza:kyiv") — not an organic-ranking input.</param>
/// <param name="StartsAt">When the placement starts being served.</param>
/// <param name="EndsAt">When the placement stops being served (must be after <paramref name="StartsAt"/>).</param>
public sealed record CreateAdPlacementCommand(
    Guid VenueId,
    string TargetingKey,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
