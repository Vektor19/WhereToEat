import type { ResultCardPhoto } from '../results/result-card.view-model';

/**
 * The fully-resolved view-models the restaurant-details surface renders. The smart container
 * ({@link RestaurantDetailsComponent}) maps the live `RestaurantDetailsDto` (from
 * `GET /restaurants/{id}`) through the Step 8 currency formatter and the catalog dish-name lookup
 * into these pure-string shapes, so the dumb presentational children render strings only and never
 * re-read the wire DTO.
 *
 * Invariants honoured at the view-model level:
 * - **contact links** are carried for **every** venue regardless of Verified status (§5.8) and are
 *   never gated/monetized (invariant #10);
 * - **menu prices** are pre-formatted by the menu item's `priceCurrency` (never a hardcoded symbol —
 *   invariant §3);
 * - **menu photos** are the generic-by-default {@link ResultCardPhoto} (invariant #8), real photos
 *   only where the backend signals permission (no such signal in the read model today → always generic);
 * - **no per-item price/photo disclaimer and no "updated X days ago"** anywhere — the single discreet
 *   footer/`ⓘ` notice (Step 2) is the sole data-accuracy notice (invariant #12 / §10).
 */
export interface RestaurantDetailsViewModel {
  /** The opaque restaurant id (view/action analytics correlation). */
  readonly restaurantId: string;
  /** The restaurant display name (the page heading). */
  readonly name: string;
  /** The full address line ("street, building"). */
  readonly addressLine: string;
  /** The city, or `null` when the backend did not supply one (the view omits it then). */
  readonly addressCity: string | null;
  /** The always-shown contact links (§5.8) — possibly empty, never gated. */
  readonly contactLinks: readonly ContactLinkViewModel[];
  /** The menu items in backend order, each with a formatted price and a generic photo. */
  readonly menuItems: readonly MenuItemViewModel[];
}

/**
 * One contact link (site / social / phone), rendered as a safe external link for **every** venue,
 * Verified or not (§5.8, invariant #10). The container resolves the user-facing `text` (the backend
 * label when present, else a kind-derived fallback) so the dumb component renders a pure string.
 */
export interface ContactLinkViewModel {
  /** The backend-supplied kind (e.g. `site`, `phone`, a social network) — open string, drives the icon. */
  readonly kind: string;
  /** The target URL (an `https`/`tel:` href). */
  readonly url: string;
  /** The visible link text (backend label, or a kind-derived fallback). */
  readonly text: string;
  /** Whether this is a `tel:` link (rendered without `target=_blank`). */
  readonly isPhone: boolean;
}

/**
 * One menu item resolved for display: the dish name (from the catalog lookup, or a neutral fallback
 * when the taxonomy is not loaded), the pre-formatted price string (by `priceCurrency` — never a
 * hardcoded symbol), an optional weight, and the generic-by-default photo (invariant #8).
 */
export interface MenuItemViewModel {
  /** The menu item id (stable list key). */
  readonly id: string;
  /** The dish name resolved from the catalog, or a neutral fallback label. */
  readonly dishName: string;
  /** The currency-formatted price (by the item's `priceCurrency`) — never null (a menu item has a price). */
  readonly price: string;
  /** The portion weight/size string, or `null` when the item carries none (the row omits it then). */
  readonly weight: string | null;
  /** The generic/real photo descriptor (generic by default — invariant #8). */
  readonly photo: ResultCardPhoto;
}
