/**
 * Barrel for the `core` data-access layer — one operation port (interface) per backend endpoint,
 * its `HttpClient` adapter, and an injection token per port (dependency inversion: features depend
 * on the token/interface, not on `HttpClient`). Re-exported through the library's public-api so the
 * `app` consumes it through the one boundary.
 */

// Public-host operations.
export * from './public/catalog.operations';
export * from './public/search.operations';
export * from './public/recommend.operation';
export * from './public/map.operation';
export * from './public/analytics-ingest.operation';
export * from './public/identity.operation';
export * from './public/rating.operation';
export * from './public/health.operation';

// Admin-host operations + their wire bodies.
export * from './admin/admin-bodies';
export * from './admin/catalog-edit.operations';
export * from './admin/protection.operations';
export * from './admin/photo.operation';
export * from './admin/monetization.operations';
export * from './admin/analytics-dashboard.operations';

// Injection tokens (one per operation).
export * from './tokens';
