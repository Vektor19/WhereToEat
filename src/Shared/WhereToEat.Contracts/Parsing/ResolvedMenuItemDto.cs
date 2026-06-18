namespace WhereToEat.Contracts.Parsing;

/// <summary>
/// One <b>mappable</b> menu item the parser normalized: a canonical <see cref="DishId"/> (already
/// resolved against the taxonomy), the price amount + ISO currency, and an optional weight/quantity
/// label. Only resolved (mappable) items reach the persist seam as live menu items — unmappable raw
/// items go to the quarantine queue instead, never here.
/// </summary>
public sealed record ResolvedMenuItemDto(
    Guid DishId,
    decimal PriceAmount,
    string PriceCurrency,
    string? Weight);
