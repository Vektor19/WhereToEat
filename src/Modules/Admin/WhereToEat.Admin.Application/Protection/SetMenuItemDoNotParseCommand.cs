namespace WhereToEat.Admin.Application.Protection;

/// <summary>
/// Sets (or clears) the per-MenuItem <c>DoNotParse</c> flag — the item-level half of the admin &gt;
/// parser protection surface (invariant #3). When on, the Step 9 parser persist gate must not
/// create/overwrite this item.
/// </summary>
public sealed record SetMenuItemDoNotParseCommand(Guid MenuItemId, bool DoNotParse);
