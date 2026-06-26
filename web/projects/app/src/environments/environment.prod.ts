import type { AppConfig } from 'core';

/**
 * Production environment configuration.
 *
 * Swapped in for `environment.ts` at build time via the `production`
 * configuration's `fileReplacements` in `angular.json`.
 *
 * In production the SPA is served same-origin behind the deployed reverse
 * proxy / API gateway, so `gatewayOrigin` stays empty and relative `/api/...`
 * paths resolve against the deployed origin. The gateway routes the two
 * logical-host prefixes to the PublicApi / AdminApi processes; CORS is handled
 * by the gateway, not by per-host backend config.
 *
 * The credential/flag values below are placeholders that the deployment
 * substitutes; no feature code reads raw hosts or credentials.
 */
export const environment: AppConfig = {
  production: true,

  // Same-origin behind the deployed gateway.
  gatewayOrigin: '',

  apiPublicPrefix: '/api/public',
  apiAdminPrefix: '/api/admin',

  auth: {
    issuer: '',
    clientId: 'wheretoeat-spa',
    redirectUri: '',
    scope: 'openid profile email',
  },

  map: {
    provider: 'google-embed',
    googleMapsEmbedKey: '',
  },

  features: {
    analyticsEnabled: true,
    ratingSubmitEnabled: true,
    portalEnabled: true,
  },
};
