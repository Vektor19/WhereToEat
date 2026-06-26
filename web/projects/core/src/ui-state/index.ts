/**
 * Barrel for the `core` UI-state cross-cutting layer — the reusable loading / async-state primitive
 * (`LoadingService` + `RequestState`) that drives a consistent spinner / error / empty-state across
 * features (DRY). Re-exported through the library public-api so the `app` consumes it through the one
 * boundary.
 */
export * from './loading.service';
