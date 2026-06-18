namespace WhereToEat.Admin.Application.MenuItems;

/// <summary>
/// Admin edit of a menu item (§4): change its price, weight, and the dish it belongs to
/// (re-categorisation = pointing the item at a dish in the target category). The edit marks the item
/// as admin-sourced provenance so a later parse treats it as curated. The admin is the source of
/// truth over the parser (invariant #3).
/// </summary>
public sealed record EditMenuItemCommand(
    Guid MenuItemId,
    Guid DishId,
    decimal PriceAmount,
    string PriceCurrency,
    string? Weight);
