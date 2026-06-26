/**
 * The typed request bodies for the Admin-host write operations, mirroring the backend wire records
 * field-for-field (`EditMenuItemBody`, `EditAddressBody`, `FlagBody`, `GrantVerifiedBody`,
 * `CreateAdPlacementBody`). Kept in one cohesive module so every admin operation references the
 * same source-of-truth body shape. Admin writes return 204 No Content on success; failures arrive
 * as the `{ error, message }` envelope and are normalized by the Step 7 interceptor.
 */

/** Body for `PUT /admin/menu-items/{id}` — edit price/weight and the dish (re-categorisation). */
export interface EditMenuItemBody {
  readonly dishId: string;
  readonly priceAmount: number;
  readonly priceCurrency: string;
  readonly weight?: string | null;
}

/** Body for `PUT /admin/restaurants/{id}/address` — the address line + optional city. */
export interface EditAddressBody {
  readonly addressLine: string;
  readonly city?: string | null;
}

/** Body for the boolean protection / permission toggles (`FlagBody` on the backend). */
export interface FlagBody {
  readonly value: boolean;
}

/**
 * The grantable Verified tiers. Mirrors the backend `SubscriptionTier` members the admin can grant
 * (`Basic` / `Pro`); the enum also has `None`, but that is "not subscribed" (a revoke, not a
 * grant), so it is excluded here. This stays a readable string union at the call sites/UI; the
 * actual wire value is the enum's integer ordinal (see {@link VERIFIED_TIER_ORDINALS}).
 */
export type VerifiedTier = 'Basic' | 'Pro';

/**
 * Maps each grantable {@link VerifiedTier} to the integer the backend `SubscriptionTier` enum binds
 * to. The Admin host uses default `System.Text.Json` with **no** `JsonStringEnumConverter`, so the
 * enum binds from its integer value, not the member name — sending the name would 400. The ordinals
 * are the source of truth from
 * `src/Modules/Monetization/WhereToEat.Monetization.Domain/SubscriptionTier.cs`
 * (`None = 0`, `Basic = 1`, `Pro = 2`).
 */
export const VERIFIED_TIER_ORDINALS: Readonly<Record<VerifiedTier, number>> = {
  Basic: 1,
  Pro: 2,
};

/**
 * Body for `PUT /admin/venues/{id}/verified` — the tier to grant (`GrantVerifiedBody`). `tier` is
 * the integer ordinal of the backend `SubscriptionTier` enum (see {@link VERIFIED_TIER_ORDINALS}),
 * not the member name, because the host's default JSON enum binder expects the integer.
 */
export interface GrantVerifiedBody {
  readonly tier: number;
}

/**
 * Body for `POST /admin/venues/{id}/ad-placements` — a targeted, time-bounded labeled slot
 * (`CreateAdPlacementBody`). `startsAt` / `endsAt` are ISO-8601 strings (the backend binds
 * `DateTimeOffset`).
 */
export interface CreateAdPlacementBody {
  readonly targetingKey: string;
  readonly startsAt: string;
  readonly endsAt: string;
}

/** The `201 Created` response of `POST /admin/venues/{id}/ad-placements` (`{ id }`). */
export interface CreatedAdPlacement {
  readonly id: string;
}
