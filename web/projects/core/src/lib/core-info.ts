/**
 * Marker for the `core` library's public surface. The real public API
 * (domain models, data-access interfaces, business-logic services, stores)
 * is populated in later steps; this keeps the library buildable and gives the
 * `app` something concrete to consume through the public-api barrel so the
 * cross-library boundary is exercised from Step 1.
 */
export const CORE_LIBRARY_NAME = 'core' as const;
