import { InjectionToken } from '@angular/core';

/**
 * Selected map provider kind. The default is the Google Maps Embed; with no
 * credential present the runtime degrades to a PlaceId / deep-link affordance
 * (design decision #11). This is the config-level **selection** (a string union),
 * distinct from the `MapProvider` **interface** in `core/map` that the concrete
 * providers implement — so the choice is never hardcoded in a component.
 */
export type MapProviderKind = 'google-embed' | 'deep-link';

/** OIDC / Keycloak configuration (wired for real in Step 6). */
export interface AuthConfig {
  /** OIDC issuer / authority URL (Keycloak realm). */
  readonly issuer: string;
  /** Public client id registered for the SPA. */
  readonly clientId: string;
  /** Redirect URI the IdP returns to after Authorization Code + PKCE. */
  readonly redirectUri: string;
  /** Space-separated OIDC scopes (e.g. `openid profile email`). */
  readonly scope: string;
}

/** Map-provider configuration (wired for real in Step 13). */
export interface MapConfig {
  /** Which provider to use; falls back to deep-link when no credential. */
  readonly provider: MapProviderKind;
  /** Google Maps Embed credential; empty triggers the deep-link fallback. */
  readonly googleMapsEmbedKey: string;
}

/** Runtime feature flags — read through config, never hardcoded in components. */
export interface FeatureFlags {
  readonly analyticsEnabled: boolean;
  readonly ratingSubmitEnabled: boolean;
  readonly portalEnabled: boolean;
}

/**
 * The single typed application configuration shape.
 *
 * Carries the gateway origin + the two logical-host route prefixes, the OIDC
 * placeholders, the map-provider selection, and feature flags. The `app`
 * supplies a concrete instance from its `environment.ts` / `environment.prod.ts`
 * via {@link APP_CONFIG}; `core` services (e.g. {@link ApiConfig}) consume it so
 * no host string or credential is hardcoded in feature code.
 *
 * Switching origins or prefixes is therefore an environment-file change only.
 */
export interface AppConfig {
  /** True for production builds. */
  readonly production: boolean;
  /**
   * The single gateway origin. Empty string = same-origin (the SPA uses
   * relative `/api/...` paths in every environment; the dev proxy or deployed
   * gateway forwards them). A non-empty value is only used when the SPA must
   * point at a remote gateway.
   */
  readonly gatewayOrigin: string;
  /** Route prefix for the Public logical host behind the gateway. */
  readonly apiPublicPrefix: string;
  /** Route prefix for the Admin logical host behind the gateway. */
  readonly apiAdminPrefix: string;
  readonly auth: AuthConfig;
  readonly map: MapConfig;
  readonly features: FeatureFlags;
}

/**
 * DI token carrying the resolved {@link AppConfig}. Provided by the `app` from
 * its environment file; injected by `core` services. Keeping the token in
 * `core` lets both the consumer and portal surfaces consume the same config
 * without crossing the consumer↔portal boundary.
 */
export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
