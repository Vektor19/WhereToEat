/**
 * Pure role-claim parsing — turns a decoded Keycloak/OIDC token-claims object into the app's own
 * {@link UserRole} set, mirroring the backend's `RoleClaimMapper`.
 *
 * Keycloak can place roles in several shapes depending on realm/client mapper configuration, so the
 * parser is deliberately tolerant and reads **all** of them (Risk #4 in the design — the exact
 * claim location is environment config, so we accept every common placement):
 *   - **realm roles** — nested under `realm_access.roles` (the Keycloak default);
 *   - **client roles** — nested under `resource_access.{client}.roles`;
 *   - **flattened** — a `role`/`roles` claim carrying a string or string[] (the backend's default
 *     `RoleClaimMapper` shape, used when a Keycloak mapper flattens roles into a top-level claim).
 *
 * Only the well-known strings `admin`/`user` are mapped (case-insensitively); unknown role strings
 * are ignored rather than failing — an IdP often carries roles we do not model. This keeps the
 * frontend's role view 1:1 with what the backend authorizes on, so a token that the Admin host
 * accepts as admin also reads as admin here.
 *
 * Kept as a framework-light pure function (no Angular DI) so it is trivially unit-tested and is a
 * clean 1:1 blueprint for the React Native rewrite.
 */
import type { UserRole } from '../domain';

/**
 * A decoded JWT claims object. The token library hands us an untyped record; we narrow the few
 * claim shapes we care about at read time rather than trusting a wide interface.
 */
export type TokenClaims = Record<string, unknown>;

/** Map a single raw role string onto a {@link UserRole}, or `undefined` if it is one we do not model. */
function mapRoleString(value: string): UserRole | undefined {
  const normalized = value.trim().toLowerCase();
  if (normalized === 'admin') {
    return 'Admin';
  }
  if (normalized === 'user') {
    return 'User';
  }
  return undefined;
}

/** True when `value` is a non-null object (and not an array). */
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Collect role strings from a claim that is either a single string or an array of strings. */
function readStringOrArray(value: unknown): string[] {
  if (typeof value === 'string') {
    return [value];
  }
  if (Array.isArray(value)) {
    return value.filter((item): item is string => typeof item === 'string');
  }
  return [];
}

/** Read the `roles` array out of a Keycloak `*_access` container (e.g. `realm_access.roles`). */
function readAccessRoles(container: unknown): string[] {
  if (!isRecord(container)) {
    return [];
  }
  return readStringOrArray(container['roles']);
}

/**
 * Parse the application's {@link UserRole} set from a decoded token-claims object, reading every
 * supported claim placement (realm-role, client-role, and flattened `role`/`roles`). Returns a
 * de-duplicated, frozen array; an empty array when no recognized role is present (or `claims` is
 * nullish).
 */
export function parseRolesFromClaims(claims: TokenClaims | null | undefined): readonly UserRole[] {
  if (!isRecord(claims)) {
    return Object.freeze([]);
  }

  const rawRoleStrings: string[] = [];

  // Keycloak realm roles: realm_access.roles
  rawRoleStrings.push(...readAccessRoles(claims['realm_access']));

  // Keycloak client roles: resource_access.{client}.roles (across all clients in the token).
  const resourceAccess = claims['resource_access'];
  if (isRecord(resourceAccess)) {
    for (const client of Object.values(resourceAccess)) {
      rawRoleStrings.push(...readAccessRoles(client));
    }
  }

  // Flattened claims (the backend's default RoleClaimMapper shape).
  rawRoleStrings.push(...readStringOrArray(claims['role']));
  rawRoleStrings.push(...readStringOrArray(claims['roles']));

  const roles = new Set<UserRole>();
  for (const raw of rawRoleStrings) {
    const mapped = mapRoleString(raw);
    if (mapped !== undefined) {
      roles.add(mapped);
    }
  }

  return Object.freeze([...roles]);
}

/**
 * Merge a token-derived role set with the `/me`-reported roles. Both are authoritative sources per
 * the step (the role can come from the Keycloak token and/or `/me`); a role present in either is
 * effective. Returns a de-duplicated, frozen array.
 */
export function mergeRoles(
  fromToken: readonly UserRole[],
  fromIdentity: readonly UserRole[],
): readonly UserRole[] {
  return Object.freeze([...new Set<UserRole>([...fromToken, ...fromIdentity])]);
}
