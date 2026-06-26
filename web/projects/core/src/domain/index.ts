/**
 * Barrel for the `core` domain models — the framework-light TypeScript types mirroring the
 * backend contracts. Re-exported through the library's public-api so the `app` consumes them
 * through the one boundary (deep imports past the barrel are forbidden by the import-boundary
 * lint rules).
 */
export * from './contract-keys';
export * from './api-error';
export * from './catalog.models';
export * from './map.models';
export * from './recommendation.models';
export * from './analytics.models';
export * from './analytics-dashboard.models';
export * from './identity.models';
