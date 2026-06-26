/**
 * Barrel for the `core` formatting layer — the locale-aware presenters: currency (formatted by the
 * backend `PriceCurrency`, never a hardcoded symbol), distance (km), and the smoothed-rating
 * presenter with its discreet low-review-count note (invariant #6). All are pure/injectable and free
 * of baked-in launch-locale copy (Step 17 wires i18n). Re-exported through the library public-api.
 */
export * from './currency.formatter';
export * from './distance.formatter';
export * from './rating.presenter';
