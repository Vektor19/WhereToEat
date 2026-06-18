namespace WhereToEat.Catalog.Application.Contracts;

/// <summary>
/// The read DTOs the public catalog endpoints serialise. They are intentionally flat, framework-free
/// records (no domain types leak to the wire) so the HTTP contract is stable independently of the
/// domain model. Money is split into amount + currency, and coordinates are returned as plain
/// numbers — all sourced purely from what we store (invariant #7: OSM-sourced only, never Google).
/// </summary>
public sealed record CategoryDto(Guid Id, string Name);

/// <summary>A dish (concrete position) within its category — the lower taxonomy level (invariant #2).</summary>
public sealed record DishDto(Guid Id, Guid CategoryId, string CanonicalName);

/// <summary>A website/social/phone link, <b>always</b> shown for free in details (§5.8, invariant #10).</summary>
public sealed record ContactLinkDto(string Kind, string Url, string? Label);

/// <summary>A menu item (dish-at-restaurant) with its price split into amount + ISO currency.</summary>
public sealed record MenuItemDto(Guid Id, Guid DishId, decimal PriceAmount, string PriceCurrency, string? Weight);

/// <summary>
/// Full restaurant details. <see cref="ContactLinks"/> is <b>always</b> populated (even for
/// non-Verified venues — §5.8); contact links are never monetized (invariant #10).
/// </summary>
public sealed record RestaurantDetailsDto(
    Guid Id,
    string Name,
    string AddressLine,
    string? AddressCity,
    IReadOnlyList<ContactLinkDto> ContactLinks,
    IReadOnlyList<MenuItemDto> MenuItems);
