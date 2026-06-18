using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Parsing.Domain;

/// <summary>
/// A raw parsed dish line the normalizer <b>could not map</b> to a canonical <c>Dish</c>, captured for
/// later admin resolution (invariant #2 — unmappable dishes are <b>quarantined, never dropped, never
/// fatal</b>). It keeps everything an admin needs to resolve the mapping: the source restaurant name +
/// address (so the item can be re-attached), the <see cref="RawName"/>/<see cref="Price"/>/
/// <see cref="Weight"/>/<see cref="CategoryHint"/> as parsed, and a <see cref="DetectedAtUtc"/>
/// timestamp. The admin module surfaces this review queue in a future step; until an admin maps the
/// raw name to a canonical dish, the item is <b>never</b> written as a live menu item.
/// </summary>
public sealed record ParseQuarantineItem(
    Guid Id,
    string RestaurantName,
    string RestaurantAddressLine,
    string RawName,
    Money Price,
    string? Weight,
    string? CategoryHint,
    DateTimeOffset DetectedAtUtc)
{
    /// <summary>
    /// Creates a quarantine item for an unmappable raw dish with a fresh id and the supplied detection
    /// time (injected so tests stay deterministic).
    /// </summary>
    public static ParseQuarantineItem ForUnmapped(
        string restaurantName,
        string restaurantAddressLine,
        ParsedDish dish,
        DateTimeOffset detectedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(dish);
        return new ParseQuarantineItem(
            Guid.NewGuid(),
            restaurantName,
            restaurantAddressLine,
            dish.RawName,
            dish.Price,
            dish.Weight,
            dish.CategoryHint,
            detectedAtUtc);
    }
}
