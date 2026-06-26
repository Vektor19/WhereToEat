import type { RatingPresentation } from 'core';

/**
 * The fully-resolved view-model one result card renders. The smart container
 * ({@link ResultsListComponent}) maps each `RecommendedRestaurantDto` (in **backend order** —
 * invariant #5) through the Step 8 formatters/presenter into this shape, so the dumb
 * {@link ResultCardComponent} renders pure strings and never re-reads the engine DTO or re-sorts.
 *
 * Key invariant honoured at the view-model level:
 * - **basket price** is a pre-formatted string (by `basketPriceCurrency`, never a hardcoded symbol)
 *   with **no `≈` marker / no per-card price disclaimer** — the single discreet footer/`ⓘ` notice
 *   (Step 2) is the sole carrier of the price-approximation disclaimer (invariant #12 / §10);
 * - **rating** is our own smoothed presentation with the low-review note (invariant #6) — never Google;
 * - **distance** is present only when geo was provided (invariant #11) and absent otherwise;
 * - **coverage** is carried as `coverageCovered`/`coverageTotal` and rendered as clearly **secondary**
 *   information, never as the ordering key (invariant #5).
 */
export interface ResultCardViewModel {
  /** The opaque restaurant id (impression/card_open correlation; details navigation later). */
  readonly restaurantId: string;
  /** The restaurant display name. */
  readonly name: string;
  /** 1-based position in the backend-ordered list (for impression/card_open analytics). */
  readonly position: number;

  /**
   * The pre-formatted basket price (currency-formatted by the DTO's `basketPriceCurrency`), or
   * `null` when the engine could not compute a basket — the card then renders no price (never "0").
   * Carries **no** `≈` marker and **no** per-card disclaimer (invariant #12 / §10).
   */
  readonly basketPrice: string | null;

  /** Our smoothed-rating presentation (value + count + low-review note key — invariant #6). */
  readonly rating: RatingPresentation;

  /**
   * The pre-formatted distance ("X km"), or `null` when geo was not provided (invariant #11) — the
   * card omits the distance row entirely rather than showing "0 km".
   */
  readonly distance: string | null;

  /** How many of the selected items this venue covers (the numerator of "N of M"). */
  readonly coverageCovered: number;
  /** The total number of selected items (the denominator of "N of M"); 0 hides coverage. */
  readonly coverageTotal: number;

  /**
   * The generic/real photo descriptor (invariant #8 — generic is our own content and the default;
   * real photos only where the backend signals permission).
   */
  readonly photo: ResultCardPhoto;

  /**
   * **Ad-slot presentation contract (invariant #10).** `false` for every organic result. When the
   * backend later adds an ad-slot field, a sponsored item carries `true` and the list renders the
   * labeled, visually-separated ad treatment. Defaults to `false` so organic results never get the
   * ad chrome and no fake ad data enters the real flow.
   */
  readonly isAd: boolean;
}

/**
 * A card's image descriptor. Generic category/dish imagery is **our own content** and the default on
 * every card (invariant #8); a real photo is used **only** where the backend signals permission — so
 * we never render a third-party photo by default.
 */
export interface ResultCardPhoto {
  /** Whether this is a real (permitted) venue photo (`true`) or our generic asset (`false`). */
  readonly isReal: boolean;
  /** The image source URL (a generic asset path, or the permitted real-photo URL). */
  readonly src: string;
  /** Accessible alt text (the dish/category/venue label). */
  readonly alt: string;
}
