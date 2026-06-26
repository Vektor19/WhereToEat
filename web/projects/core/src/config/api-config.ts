import { Injectable, inject } from '@angular/core';

import { APP_CONFIG } from './app-config.token';

/**
 * Resolves URLs for the two logical hosts (Public vs Admin) behind the single
 * gateway origin.
 *
 * The SPA addresses the backend with relative `/api/...` paths; this service is
 * the ONE place that turns an endpoint path into a full request path by
 * prepending the correct logical-host route prefix (`apiPublicPrefix` /
 * `apiAdminPrefix`) — and, only when a remote gateway origin is configured, the
 * origin. The data-access layer (Step 5) depends on this so no host string is
 * hardcoded in feature code; switching origin/prefix is an environment change.
 *
 * URL composition is host-relative: with the default empty `gatewayOrigin` the
 * result is a relative path (e.g. `/api/public/categories`) that the dev proxy
 * or deployed gateway forwards — it never emits an absolute cross-origin host.
 */
@Injectable({ providedIn: 'root' })
export class ApiConfig {
  private readonly config = inject(APP_CONFIG);

  /** The resolved Public-host route prefix (e.g. `/api/public`). */
  get publicPrefix(): string {
    return this.config.apiPublicPrefix;
  }

  /** The resolved Admin-host route prefix (e.g. `/api/admin`). */
  get adminPrefix(): string {
    return this.config.apiAdminPrefix;
  }

  /**
   * Compose a request URL for the Public logical host.
   *
   * @param path endpoint path as the backend exposes it at its root, with a
   *   leading slash (e.g. `/categories`, `/recommend`). The Public prefix is
   *   prepended so the gateway forwards it to PublicApi.
   */
  publicUrl(path: string): string {
    return this.compose(this.config.apiPublicPrefix, path);
  }

  /**
   * Compose a request URL for the Admin logical host.
   *
   * @param path endpoint path as the Admin host exposes it (already including
   *   its `/admin/...` segment, e.g. `/admin/menu-items/{id}`). The Admin
   *   prefix is prepended so the gateway forwards it to AdminApi.
   */
  adminUrl(path: string): string {
    return this.compose(this.config.apiAdminPrefix, path);
  }

  /**
   * Join the (optional) gateway origin, a logical-host prefix, and an endpoint
   * path into a single URL, normalizing slashes so there are never doubled or
   * missing separators regardless of how callers format the inputs.
   */
  private compose(prefix: string, path: string): string {
    const origin = this.trimTrailingSlash(this.config.gatewayOrigin);
    const normalizedPrefix = this.ensureLeadingSlash(this.trimTrailingSlash(prefix));
    const normalizedPath = this.ensureLeadingSlash(path);
    return `${origin}${normalizedPrefix}${normalizedPath}`;
  }

  private ensureLeadingSlash(value: string): string {
    if (value.length === 0) {
      return '';
    }
    return value.startsWith('/') ? value : `/${value}`;
  }

  private trimTrailingSlash(value: string): string {
    return value.endsWith('/') ? value.slice(0, -1) : value;
  }
}
