/**
 * Route guard for the operator portal: admits only a session carrying the `Admin` role (the
 * Keycloak admin/operator role, read from the token and/or `/me` via {@link AuthService}).
 *
 * A non-admin token and an anonymous (unauthenticated) caller are both denied. When the caller is
 * not signed in at all the guard kicks off the Authorization Code + PKCE login (best-effort) so the
 * portal entry point doubles as a sign-in prompt; an authenticated-but-non-admin caller is simply
 * refused (the Admin host also enforces the policy server-side, returning 403). Anonymous consumer
 * routes do not use this guard, so public reads stay open.
 *
 * Lives in `core` so the portal can consume it without importing consumer/portal-internal wiring.
 */
import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';

import { AuthService } from './auth.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);

  if (auth.isAdmin()) {
    return true;
  }

  // Not an admin: if the caller is not even signed in, start the login flow so the portal route
  // acts as the sign-in entry point. Either way the route is blocked for now.
  if (!auth.isAuthenticated()) {
    void auth.login();
  }

  return false;
};
