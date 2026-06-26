/**
 * Barrel for the `core` auth foundation — the OIDC session façade, the bearer interceptor, the
 * portal route guard, and the pure role/JWT helpers. Re-exported through the library public-api so
 * both surfaces (consumer rating-submit, operator portal) consume the same session through the one
 * boundary.
 */
export * from './auth.service';
export * from './bearer.interceptor';
export * from './admin.guard';
export * from './role-claims';
export * from './jwt';
