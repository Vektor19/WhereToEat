/**
 * The typed application error model. The backend reports every failure with the same envelope —
 * `{ error, message }` (the `error` is a stable machine code like `Analytics.UnknownKind`, the
 * `message` is a human-readable description). This module defines that wire envelope plus the
 * normalized in-app error the error interceptor (Step 7) maps every `HttpErrorResponse` onto, so
 * feature code never touches a raw `HttpErrorResponse`.
 */

/** The backend failure envelope as it arrives on the wire (`{ error, message }`). */
export interface ApiErrorEnvelope {
  readonly error: string;
  readonly message: string;
}

/**
 * The broad category an HTTP failure falls into, derived from the status code by the error
 * interceptor. Keeps feature code reasoning about *kinds* of failure rather than raw status
 * numbers (e.g. show a sign-in prompt for `unauthorized`, a not-found state for `not-found`).
 */
export type ApiErrorKind =
  | 'validation' // 400 — bad request / failed validation
  | 'unauthorized' // 401 — no/invalid token
  | 'forbidden' // 403 — authenticated but not permitted (e.g. non-admin on the Admin host)
  | 'not-found' // 404
  | 'server' // 5xx
  | 'network' // transport failure (status 0 / offline)
  | 'unknown'; // anything else

/**
 * The i18n message-key for a failure's user-facing copy. The error interceptor (Step 7) derives
 * one of these from the `kind` and stamps it onto every {@link ApiError}; Step 17 wires the i18n
 * catalogs that resolve a key to localized copy. Keeping the key here — rather than a hardcoded
 * UA/EN string — means the cross-cutting layer never bakes launch-locale copy into the error model
 * and Step 17 can localize without touching this layer (invariant: Ukrainian-first, localization-
 * ready).
 */
export type ApiErrorMessageKey =
  | 'errors.validation'
  | 'errors.unauthorized'
  | 'errors.forbidden'
  | 'errors.notFound'
  | 'errors.server'
  | 'errors.network'
  | 'errors.unknown';

/**
 * The normalized application error. `code`/`message` come from the backend `{ error, message }`
 * envelope when present (otherwise a sensible default); `kind` is derived from the status;
 * `messageKey` is the i18n key for the user-facing copy (resolved by Step 17, never a hardcoded
 * localized string here); `status` is the raw HTTP status (0 for a transport failure). This is the
 * only error type feature code sees.
 */
export interface ApiError {
  readonly kind: ApiErrorKind;
  readonly status: number;
  readonly code: string;
  readonly message: string;
  readonly messageKey: ApiErrorMessageKey;
}

/** Narrowing guard: a value is the backend `{ error, message }` envelope. */
export function isApiErrorEnvelope(value: unknown): value is ApiErrorEnvelope {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as Record<string, unknown>)['error'] === 'string' &&
    typeof (value as Record<string, unknown>)['message'] === 'string'
  );
}

/**
 * Narrowing guard: a value is a normalized {@link ApiError}. The error-normalization interceptor
 * (Step 7) guarantees every HTTP failure reaches feature code as an `ApiError`, but non-HTTP
 * observable sources (e.g. a thrown string in a synchronous pipe) can surface other values; this
 * guard lets the cross-cutting layer reason about an `unknown` error safely.
 */
export function isApiError(value: unknown): value is ApiError {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate['kind'] === 'string' &&
    typeof candidate['status'] === 'number' &&
    typeof candidate['code'] === 'string' &&
    typeof candidate['message'] === 'string' &&
    typeof candidate['messageKey'] === 'string'
  );
}
