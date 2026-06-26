/**
 * §7.5 operator-dashboard **aggregate-only** DTOs (Step 24), mirroring the Step 23 backend
 * `IAnalyticsRollupReader` records field-for-field (camelCase wire casing — the Admin host serializes
 * with the default minimal-API camelCase policy).
 *
 * **Invariant #11 is enforced by shape.** Every type here is a counts/ratios aggregate: there is no
 * actor hash, no per-event id, no precise coordinate, and no per-user row — the seam that produces
 * these can never expose them, so the frontend can never receive or reconstruct per-user/per-event
 * data. A `selectionId` in {@link DemandCountDto} identifies a taxonomy item (a category or dish,
 * invariant #2), never a person.
 */

/**
 * Visibility / traffic for one restaurant over a window: total impressions, card-opens, action
 * clicks (look-on-map / phone / site / social), and the impression→card-open click-through ratio.
 * Counts/ratios only.
 */
export interface RestaurantTrafficSummaryDto {
  readonly restaurantId: string;
  readonly impressions: number;
  readonly cardOpens: number;
  readonly actions: number;
  /** The impression→card-open click-through ratio (0..1). */
  readonly ctr: number;
}

/** One demand row: how many search/selection events referenced a single category or dish id. */
export interface DemandCountDto {
  /** A taxonomy item id (category or dish), never a person (invariant #2). */
  readonly selectionId: string;
  readonly searchCount: number;
}

/**
 * Demand by category/dish over a window: the most-searched taxonomy items (area-wide, even for items
 * a venue does not carry). Grouped counts per non-identifying selection id, capped to the top-N.
 */
export interface DemandBreakdownDto {
  readonly categories: readonly DemandCountDto[];
  readonly dishes: readonly DemandCountDto[];
}

/**
 * One dish's price position: the venue's price, the market median for that dish (null when no median
 * has been materialized), and the signed delta (venue − median; negative = cheaper than market).
 * Currency follows the venue's own menu-item currency.
 */
export interface DishPricePositionDto {
  readonly dishId: string;
  readonly venuePrice: number;
  readonly medianPrice: number | null;
  readonly deltaFromMedian: number | null;
  readonly currency: string;
}

/**
 * Price positioning for one restaurant: each carried dish's price against the area/category median
 * the engine materializes. No behavioural/per-user data — purely the venue's prices and the medians.
 */
export interface PricePositioningDto {
  readonly restaurantId: string;
  readonly dishes: readonly DishPricePositionDto[];
}

/**
 * Ratings distribution for one restaurant: the cumulative all-time rating count, score sum, and raw
 * average (0 when there are no ratings) from the materialized aggregate (invariant #6). Aggregate-only
 * — never a single user's score.
 */
export interface RatingsDistributionDto {
  readonly restaurantId: string;
  readonly ratingCount: number;
  readonly scoreSum: number;
  readonly averageScore: number;
}

/**
 * Conversion funnel for one restaurant over a window: impression → card-open → action stage counts
 * and the two stage-to-stage ratios (open rate, action rate). Counts/ratios only — not a traceable
 * per-user journey (invariant #11).
 */
export interface ConversionFunnelDto {
  readonly restaurantId: string;
  readonly impressions: number;
  readonly cardOpens: number;
  readonly actions: number;
  /** impression → card-open conversion ratio (0..1). */
  readonly impressionToCardOpenRate: number;
  /** card-open → action conversion ratio (0..1). */
  readonly cardOpenToActionRate: number;
}
