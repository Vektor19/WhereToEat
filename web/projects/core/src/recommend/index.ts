/**
 * Barrel for the `core` recommendation business-logic layer — the request builder that turns a
 * selection + match + sort + filters + optional geo into the exact recommend request shape, with
 * fail-fast validation. Re-exported through the library public-api so the `app` consumes it through
 * the one boundary.
 */
export * from './recommend-request.builder';
