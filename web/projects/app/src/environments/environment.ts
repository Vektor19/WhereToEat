import type { AppConfig } from 'core';

/**
 * Development environment configuration.
 *
 * The SPA talks to ONE origin and uses relative `/api/...` paths in every
 * environment (design: "Single origin via a reverse proxy / API gateway").
 * In development the Angular dev-server proxy (`web/proxy.conf.json`) maps the
 * two logical-host prefixes behind that single origin to the two backend ports
 * (PublicApi / AdminApi). CORS is therefore an infrastructure concern, not
 * per-host backend config.
 *
 * `gatewayOrigin` is empty (same-origin) here: relative paths resolve against
 * the dev-server origin, which the proxy then forwards. The two logical hosts
 * are distinguished ONLY by route prefix (`apiPublicPrefix` / `apiAdminPrefix`)
 * — no cross-origin host string lives in feature code.
 *
 * Keycloak / Google Maps Embed credentials and feature flags are placeholders
 * here; they are wired for real in later steps (auth in Step 6, map in Step 13)
 * and supplied per deployment without touching feature code.
 */
export const environment: AppConfig = {
  production: false,

  // Single gateway origin. Empty = same-origin; relative `/api/...` paths are
  // forwarded by the dev proxy. Switching to a remote gateway is a one-line
  // change here (no feature-code change), satisfying the step's acceptance.
  gatewayOrigin: '',

  // The two logical hosts behind the gateway, addressed by route prefix only.
  apiPublicPrefix: '/api/public',
  apiAdminPrefix: '/api/admin',

  // OIDC / Keycloak — placeholders, wired in Step 6. The dev IdP is the local
  // Keycloak from deploy/docker-compose.yml (host port 8088 → realm
  // `wheretoeat`). Concrete client id / redirect URIs are confirmed there.
  auth: {
    issuer: 'http://localhost:8088/realms/wheretoeat',
    clientId: 'wheretoeat-spa',
    redirectUri: 'http://localhost:4200/',
    scope: 'openid profile email',
  },

  // Map provider — placeholder, wired in Step 13. With no Embed key the
  // provider degrades to a PlaceId / Maps deep-link affordance (design #11).
  map: {
    provider: 'google-embed',
    googleMapsEmbedKey: '',
  },

  // Feature flags — never hardcoded in components; read through AppConfig.
  features: {
    analyticsEnabled: true,
    ratingSubmitEnabled: true,
    portalEnabled: true,
  },
};
