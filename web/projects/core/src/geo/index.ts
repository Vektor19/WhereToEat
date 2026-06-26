/**
 * Barrel for the `core` geo layer — the Haversine display helper ("X km from you", invariant #7/#11)
 * mirroring the backend's great-circle math for the client-side display case. Re-exported through the
 * library public-api so the `app` consumes it through the one boundary.
 */
export * from './haversine';
export * from './geolocation.service';
export * from './geo-optin.store';
