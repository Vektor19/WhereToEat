/**
 * Bearer-token HTTP interceptor (functional, registered via `withInterceptors`).
 *
 * Attaches the OIDC access token ONLY to calls that need it, and never to anything else:
 *   - **all Admin-host calls** — matched by the configured admin route prefix (`/api/admin`), since
 *     every Admin endpoint is admin-policy-gated server-side;
 *   - **an explicit allowlist of authenticated Public-host calls** — `/me` today, plus the future
 *     rating-submit. These ride the Public prefix but require a user token, so a prefix rule alone is
 *     not enough; the allowlist is matched against the Public-prefixed path. (The analytics-dashboard
 *     read is **not** here: it lives on the Admin host and already gets its token via the admin-prefix
 *     branch — see Step 23.)
 *
 * Everything else — the anonymous public reads (categories/dishes/search/recommend/map/restaurant/
 * analytics-ingest/health) and any non-API or cross-origin URL — is left untouched, so an anonymous
 * consumer read never carries a bearer.
 *
 * The "which requests get the token" rule is therefore robust: it is driven by the resolved logical-
 * host prefixes from {@link ApiConfig} (no hardcoded host) plus a small, explicit allowlist, and it
 * refuses to attach a token to absolute cross-origin URLs.
 */
import { inject } from '@angular/core';
import type {
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../config/api-config';

import { AuthService } from './auth.service';

/**
 * Public-host paths that require a bearer even though they share the Public prefix. Each rule is matched
 * against the request path with the Public route-prefix stripped. Anonymous public reads are
 * intentionally absent, and Admin-host reads (e.g. the analytics dashboard) are not here — they get
 * their token via the admin-prefix branch.
 *
 * Two rule shapes:
 *   - **prefix** (`/me`): the relative path equals the rule or starts with `rule/`.
 *   - **suffix** (`/ratings`): the relative path ends with the rule — Step 22's rating-submit endpoint
 *     is `POST /restaurants/{restaurantId}/ratings`, so the authenticated path is `…/{id}/ratings`, NOT
 *     a top-level `/ratings`. A suffix rule attaches the bearer to that nested write while the anonymous
 *     `GET /restaurants/{id}` details read (which has no `/ratings` suffix) stays token-free.
 */
interface AuthenticatedPublicRule {
  readonly value: string;
  readonly match: 'prefix' | 'suffix';
}

const AUTHENTICATED_PUBLIC_RULES: readonly AuthenticatedPublicRule[] = [
  { value: '/me', match: 'prefix' },
  { value: '/ratings', match: 'suffix' },
];

/** True when `url` targets the same origin as the document (or is a root-relative path). */
function isSameOrigin(url: string): boolean {
  // Root-relative paths (`/api/...`) are always same-origin.
  if (url.startsWith('/')) {
    return true;
  }
  try {
    const resolved = new URL(url, document.baseURI);
    return resolved.origin === window.location.origin;
  } catch {
    return false;
  }
}

/** Extract the path portion of a (possibly absolute, possibly relative) request URL. */
function pathOf(url: string): string {
  if (url.startsWith('/')) {
    return url;
  }
  try {
    return new URL(url, document.baseURI).pathname;
  } catch {
    return url;
  }
}

/** Decide whether the request should carry a bearer token. */
function shouldAttachToken(req: HttpRequest<unknown>, api: ApiConfig): boolean {
  // Never attach to a cross-origin URL — the token is for our gateway only.
  if (!isSameOrigin(req.url)) {
    return false;
  }

  const path = pathOf(req.url);
  const adminPrefix = api.adminPrefix;
  const publicPrefix = api.publicPrefix;

  // Every Admin-host call is authenticated.
  if (path.startsWith(adminPrefix)) {
    return true;
  }

  // Authenticated Public calls: match the allowlist against the path with the Public prefix removed.
  if (path.startsWith(publicPrefix)) {
    const relative = path.slice(publicPrefix.length);
    return AUTHENTICATED_PUBLIC_RULES.some((rule) =>
      rule.match === 'suffix'
        ? relative === rule.value || relative.endsWith(rule.value)
        : relative === rule.value || relative.startsWith(`${rule.value}/`),
    );
  }

  // Any other path (non-API) gets no token.
  return false;
}

export const bearerInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  const api = inject(ApiConfig);
  const auth = inject(AuthService);

  if (!shouldAttachToken(req, api)) {
    return next(req);
  }

  const token = auth.accessToken();
  if (token.length === 0) {
    // No token available (e.g. not signed in): issue the request as-is so the backend returns its
    // own 401, rather than sending an empty/forged Authorization header.
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
