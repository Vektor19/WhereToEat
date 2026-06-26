/**
 * Live-stack gating for the authenticated e2e journeys.
 *
 * Two of the three key journeys — rating-submit (authenticated public) and the admin portal +
 * analytics dashboard (admin-policy-gated) — require the full runtime: the two backend hosts
 * (PublicApi :8080 / AdminApi :8081) behind the dev gateway, a SQL database, and a Keycloak
 * realm to mint the user/admin tokens via OIDC Authorization Code + PKCE. That stack is the
 * `deploy/` docker-compose environment; it is not always available (e.g. Docker / Keycloak may
 * be absent in CI or a developer box without `.env` populated).
 *
 * Rather than fake these journeys with a mocked OIDC session (which would not exercise the real
 * Keycloak redirect/PKCE flow these specs exist to cover), they are **gated behind
 * `E2E_LIVE_STACK=1`** and the Keycloak credentials below. When the flag is unset the specs
 * `test.skip` with a descriptive reason, so a run that cannot reach the stack reports
 * "skipped (live stack not configured)" instead of a misleading pass or a hang.
 *
 * To run them, bring up `deploy/` (gateway + both hosts + DB + Keycloak), seed a user and an
 * admin, then:
 *   E2E_LIVE_STACK=1 \
 *   E2E_USER_USERNAME=... E2E_USER_PASSWORD=... \
 *   E2E_ADMIN_USERNAME=... E2E_ADMIN_PASSWORD=... \
 *   E2E_RESTAURANT_ID=<seeded-guid> \
 *   npm run e2e
 */

/** True when the live docker-compose stack + Keycloak is configured for the authed journeys. */
export const liveStackEnabled = process.env['E2E_LIVE_STACK'] === '1';

/** Credentials/ids for the live-stack journeys, read from env (never hardcoded). */
export const liveStack = {
  userUsername: process.env['E2E_USER_USERNAME'] ?? '',
  userPassword: process.env['E2E_USER_PASSWORD'] ?? '',
  adminUsername: process.env['E2E_ADMIN_USERNAME'] ?? '',
  adminPassword: process.env['E2E_ADMIN_PASSWORD'] ?? '',
  /** A restaurant id seeded in the live DB to rate / manage / view analytics for. */
  restaurantId: process.env['E2E_RESTAURANT_ID'] ?? '',
};

/** A human-readable skip reason listing what the gated journeys need. */
export const LIVE_STACK_SKIP_REASON =
  'Live stack not configured: set E2E_LIVE_STACK=1 plus the Keycloak/user/admin/restaurant ' +
  'env vars and bring up deploy/ (PublicApi 8080 + AdminApi 8081 + DB + Keycloak) to run this ' +
  'authenticated journey.';
