namespace WhereToEat.Admin.Application.Protection;

/// <summary>
/// Sets (or clears) the restaurant-level <c>DoNotUpdate</c> flag — the venue-level half of the
/// admin &gt; parser protection surface (invariant #3). When on, the Step 9 parser must skip the
/// whole venue so its hand-curated data is never overwritten.
/// </summary>
public sealed record SetRestaurantDoNotUpdateCommand(Guid RestaurantId, bool DoNotUpdate);
