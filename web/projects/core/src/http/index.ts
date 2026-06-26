/**
 * Barrel for the `core` cross-cutting HTTP layer — the error-normalization interceptor that maps
 * every `HttpErrorResponse` (and the backend `{ error, message }` envelope) onto the typed
 * {@link ApiError} model, so feature code never touches a raw HTTP failure. Re-exported through the
 * library public-api so the `app` registers the interceptor through the one boundary.
 */
export * from './error.interceptor';
