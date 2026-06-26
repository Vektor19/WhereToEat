/**
 * Error-normalization HTTP interceptor (functional, registered via `withInterceptors`).
 *
 * Every failed response — whether it carries the backend `{ error, message }` envelope or is a
 * raw transport failure — is mapped here onto the single typed {@link ApiError} model, so **feature
 * code never sees a raw `HttpErrorResponse`** (Step 7 acceptance criterion). The interceptor:
 *
 *   - derives the {@link ApiErrorKind} from the HTTP status (400 → `validation`, 401 →
 *     `unauthorized`, 403 → `forbidden`, 404 → `not-found`, 5xx → `server`, status 0 / offline →
 *     `network`, anything else → `unknown`);
 *   - pulls `code`/`message` from the `{ error, message }` envelope when the backend supplied one,
 *     otherwise falls back to a stable per-kind default code and the raw status text;
 *   - stamps the per-kind i18n {@link ApiErrorMessageKey} for the user-facing copy (Step 17 resolves
 *     the catalogs — no launch-locale string is baked in here);
 *   - re-throws the typed error so callers' RxJS error paths still fire (errors are **normalized,
 *     not swallowed**).
 *
 * Ordering: the registration order in `app.config.ts` is `[bearerInterceptor,
 * errorNormalizationInterceptor]`. On the **request** path that order runs outer→inner, so the
 * bearer interceptor attaches the token before this one. On the **response/error** path the chain
 * reverses (inner→outer): this interceptor is the **inner** one, so its `catchError` fires **first**
 * — every failure is normalized into the typed {@link ApiError} *before* it propagates back out
 * through the bearer interceptor and on to feature code. The bearer interceptor has no response-side
 * logic, so by the time an error reaches feature code it is already normalized.
 */
import { HttpErrorResponse } from '@angular/common/http';
import type {
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { catchError, throwError, type Observable } from 'rxjs';

import {
  isApiErrorEnvelope,
  type ApiError,
  type ApiErrorKind,
  type ApiErrorMessageKey,
} from '../domain/api-error';

/** A raw HttpErrorResponse has this shape; we only read these fields. */
interface HttpFailureLike {
  readonly status: number;
  readonly statusText?: string;
  readonly error?: unknown;
}

/** Per-kind metadata: the fallback `code` (used when no envelope is present) and the i18n key. */
interface KindMeta {
  readonly defaultCode: string;
  readonly messageKey: ApiErrorMessageKey;
}

const KIND_META: Readonly<Record<ApiErrorKind, KindMeta>> = {
  validation: { defaultCode: 'Http.BadRequest', messageKey: 'errors.validation' },
  unauthorized: { defaultCode: 'Http.Unauthorized', messageKey: 'errors.unauthorized' },
  forbidden: { defaultCode: 'Http.Forbidden', messageKey: 'errors.forbidden' },
  'not-found': { defaultCode: 'Http.NotFound', messageKey: 'errors.notFound' },
  server: { defaultCode: 'Http.ServerError', messageKey: 'errors.server' },
  network: { defaultCode: 'Http.Network', messageKey: 'errors.network' },
  unknown: { defaultCode: 'Http.Unknown', messageKey: 'errors.unknown' },
};

/** Map a raw HTTP status onto the broad {@link ApiErrorKind} feature code reasons about. */
function kindForStatus(status: number): ApiErrorKind {
  // Status 0 is Angular's marker for a transport-level failure (offline / CORS / aborted).
  if (status === 0) {
    return 'network';
  }
  switch (status) {
    case 400:
      return 'validation';
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    case 404:
      return 'not-found';
    default:
      return status >= 500 ? 'server' : 'unknown';
  }
}

/**
 * Normalize any HTTP failure into the typed {@link ApiError}. Exported so non-HTTP callers (and
 * tests) can reuse the exact same mapping; the interceptor is a thin wrapper over this.
 */
export function normalizeHttpError(failure: HttpFailureLike): ApiError {
  const status = failure.status;
  const kind = kindForStatus(status);
  const meta = KIND_META[kind];

  if (isApiErrorEnvelope(failure.error)) {
    return {
      kind,
      status,
      code: failure.error.error,
      message: failure.error.message,
      messageKey: meta.messageKey,
    };
  }

  // No backend envelope (transport failure, non-JSON body, or a bare status): fall back to a stable
  // per-kind code and the status text so callers still get a meaningful, typed error.
  const fallbackMessage =
    typeof failure.statusText === 'string' && failure.statusText.length > 0
      ? failure.statusText
      : meta.defaultCode;
  return {
    kind,
    status,
    code: meta.defaultCode,
    message: fallbackMessage,
    messageKey: meta.messageKey,
  };
}

export const errorNormalizationInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> =>
  next(req).pipe(
    catchError((error: unknown) => {
      // Angular's HttpClient surfaces failures as HttpErrorResponse, but guard the type instead of
      // an unchecked cast: a non-HttpErrorResponse value still maps cleanly through the `status: 0`
      // (network) branch, so the interceptor never throws on a malformed error.
      const failure: HttpFailureLike =
        error instanceof HttpErrorResponse
          ? error
          : ({ status: 0, statusText: 'Unknown', error } satisfies HttpFailureLike);
      return throwError(() => normalizeHttpError(failure));
    }),
  );
