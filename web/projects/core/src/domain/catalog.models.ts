/**
 * Catalog read models — the TypeScript mirror of the backend's public catalog DTOs
 * (`WhereToEat.Catalog.Application.Contracts.CatalogReadModels`). These are the shapes the
 * public catalog endpoints serialise; field names match the camelCased wire form (the .NET
 * minimal APIs use the default camelCase JSON policy), so this is the single source of truth
 * for the wire shape the data-access layer (Step 5) reads.
 *
 * GUIDs cross the wire as strings (TS has no GUID type). Money is split into amount + ISO
 * currency — the UI formats by `priceCurrency`, never a hardcoded symbol (invariant §3).
 */

/** A taxonomy category (the upper level — a group of dishes, invariant #2). */
export interface CategoryDto {
  readonly id: string;
  readonly name: string;
}

/** A dish (concrete position) within its category — the lower taxonomy level (invariant #2). */
export interface DishDto {
  readonly id: string;
  readonly categoryId: string;
  readonly canonicalName: string;
}

/**
 * A website / social / phone link, **always** shown for free in details (§5.8, invariant #10).
 * `kind` is a backend-supplied string (e.g. `site`, `phone`, a social network) — kept open here
 * because the backend does not constrain it to a closed enum.
 */
export interface ContactLinkDto {
  readonly kind: string;
  readonly url: string;
  readonly label?: string | null;
}

/** A menu item (dish-at-restaurant) with its price split into amount + ISO currency. */
export interface MenuItemDto {
  readonly id: string;
  readonly dishId: string;
  readonly priceAmount: number;
  readonly priceCurrency: string;
  readonly weight?: string | null;
}

/**
 * Full restaurant details. `contactLinks` is **always** populated (even for non-Verified venues —
 * §5.8); contact links are never monetized (invariant #10).
 */
export interface RestaurantDetailsDto {
  readonly id: string;
  readonly name: string;
  readonly addressLine: string;
  readonly addressCity?: string | null;
  readonly contactLinks: readonly ContactLinkDto[];
  readonly menuItems: readonly MenuItemDto[];
}
