/**
 * Barrel for the `core` validation layer — the recommendation-request validation service that
 * mirrors the backend's pre-pipeline rules (exactly-one-of-id per item, non-empty selection, known
 * match/sort/filter keys, `userGeo` range) so the UI fails fast before a round-trip. Re-exported
 * through the library public-api so the `app` consumes it through the one boundary.
 */
export * from './recommend-validation.service';
