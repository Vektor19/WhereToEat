/**
 * Auth/session façade over `angular-oauth2-oidc` for OIDC Authorization Code + PKCE against Keycloak.
 *
 * Responsibilities:
 *   - Configure the OIDC client from {@link APP_CONFIG} (issuer/authority, clientId, redirectUri,
 *     scopes) — NOTHING is hardcoded; the real Keycloak realm/authority stays in environment config.
 *   - Run the Authorization Code + PKCE flow (`login`) and clear the session (`logout`), with silent
 *     token renewal so a live session keeps a fresh access token.
 *   - Expose reactive **signals** the rest of the app gates on: {@link isAuthenticated},
 *     {@link subject} (the IdP `sub`), and {@link roles} (parsed from the access-token role claims,
 *     handling realm-role / client-role / flattened placements — see {@link parseRolesFromClaims}).
 *   - Hand the raw access token to the bearer interceptor via {@link accessToken}.
 *   - Optionally fold the `/me`-reported roles into the role set ({@link refreshFromIdentity}) so the
 *     role view matches what the backend authorizes on regardless of token claim shape.
 *
 * Kept in `core` so both the consumer (rating-submit) and portal (admin) surfaces consume the same
 * session without crossing the consumer↔portal boundary.
 */
import { Injectable, computed, inject, signal, type Signal } from '@angular/core';
import { OAuthService, AuthConfig as OidcAuthConfig } from 'angular-oauth2-oidc';
import { filter } from 'rxjs';

import { APP_CONFIG } from '../config/app-config.token';
import type { UserRole } from '../domain';

import { decodeJwtClaims } from './jwt';
import { mergeRoles, parseRolesFromClaims } from './role-claims';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly oauth = inject(OAuthService);
  private readonly appConfig = inject(APP_CONFIG);

  /** Roles derived from the current access token's claims. */
  private readonly tokenRoles = signal<readonly UserRole[]>([]);
  /** Roles reported by the `/me` probe (optional second source — see {@link refreshFromIdentity}). */
  private readonly identityRoles = signal<readonly UserRole[]>([]);
  /** Whether a valid access token is currently held. */
  private readonly authed = signal<boolean>(false);
  /** The authenticated subject (`sub`), or `null` when unauthenticated. */
  private readonly subjectId = signal<string | null>(null);

  /** True when a valid access token is held. */
  readonly isAuthenticated: Signal<boolean> = this.authed.asReadonly();

  /** The IdP subject (`sub`) of the authenticated user, or `null`. */
  readonly subject: Signal<string | null> = this.subjectId.asReadonly();

  /** The effective application roles: the union of token-derived and `/me`-reported roles. */
  readonly roles: Signal<readonly UserRole[]> = computed(() =>
    mergeRoles(this.tokenRoles(), this.identityRoles()),
  );

  /** True when the session carries the `Admin` role (portal/analytics gating). */
  readonly isAdmin: Signal<boolean> = computed(() => this.roles().includes('Admin'));

  /**
   * Configure the OIDC client and complete any in-flight redirect, then set up silent refresh. Call
   * once during app initialization (an `APP_INITIALIZER` / provider in `app.config.ts`). Returns when
   * the discovery document is loaded and any Authorization Code callback has been processed.
   *
   * **Must not block bootstrap when Keycloak is unreachable.** `loadDiscoveryDocumentAndTryLogin`
   * rejects on any discovery HTTP error (network/404/timeout); we swallow that and start the app
   * **unauthenticated** so anonymous consumer reads keep working when the IdP is down. The session
   * stays empty until the user explicitly signs in (which retries discovery).
   */
  async init(): Promise<void> {
    this.oauth.configure(this.toOidcConfig());
    this.oauth.setupAutomaticSilentRefresh();
    // Keep the session signals live across the token lifecycle: a background silent refresh hands the
    // OAuth lib a new token without our code calling `syncFromToken`, so re-derive on the relevant
    // events (new/renewed token, and clearing on expiry/logout).
    this.oauth.events
      .pipe(
        filter((event) =>
          ['token_received', 'token_refreshed', 'token_expires', 'logout'].includes(event.type),
        ),
      )
      .subscribe(() => this.syncFromToken());

    try {
      await this.oauth.loadDiscoveryDocumentAndTryLogin();
    } catch {
      // Keycloak unreachable / discovery failed: leave the app unauthenticated rather than blocking
      // bootstrap. `syncFromToken` below resolves the (empty) session state from the held token.
    }
    this.syncFromToken();
  }

  /**
   * Begin the Authorization Code + PKCE flow. If the discovery document is not yet loaded it is
   * loaded first; an already-valid session short-circuits without re-prompting.
   */
  async login(): Promise<void> {
    await this.oauth.loadDiscoveryDocumentAndLogin();
    this.syncFromToken();
  }

  /** Clear the local session (and redirect to the IdP end-session endpoint when configured). */
  logout(): void {
    this.oauth.logOut();
    this.tokenRoles.set([]);
    this.identityRoles.set([]);
    this.authed.set(false);
    this.subjectId.set(null);
  }

  /** The raw access token for the bearer interceptor, or `''` when none is held. */
  accessToken(): string {
    return this.oauth.getAccessToken() ?? '';
  }

  /**
   * Fold the roles reported by `GET /me` into the effective role set. Used to verify the auth seam
   * end-to-end and to cover IdP configurations whose token claim shape differs from what the backend
   * flattens onto `/me`.
   */
  refreshFromIdentity(roles: readonly UserRole[]): void {
    this.identityRoles.set(Object.freeze([...roles]));
  }

  /** Re-derive auth state, subject and roles from the currently-held token. */
  syncFromToken(): void {
    const valid = this.oauth.hasValidAccessToken();
    this.authed.set(valid);

    if (!valid) {
      this.tokenRoles.set([]);
      this.subjectId.set(null);
      return;
    }

    const claims = decodeJwtClaims(this.oauth.getAccessToken());
    this.tokenRoles.set(parseRolesFromClaims(claims));

    const sub = claims?.['sub'];
    this.subjectId.set(typeof sub === 'string' ? sub : null);
  }

  /** Translate the app's {@link AuthConfig} into the `angular-oauth2-oidc` config shape. */
  private toOidcConfig(): OidcAuthConfig {
    const { auth } = this.appConfig;
    return {
      issuer: auth.issuer,
      clientId: auth.clientId,
      redirectUri: auth.redirectUri,
      responseType: 'code',
      scope: auth.scope,
      // Authorization Code + PKCE — no client secret in a public SPA client.
      requireHttps: this.appConfig.production,
      showDebugInformation: !this.appConfig.production,
    };
  }
}
