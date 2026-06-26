/**
 * Identity model — the TypeScript mirror of the authenticated `GET /me` probe response on the
 * Public host (`AuthProbeEndpoints`). The endpoint echoes the caller's mapped subject and roles
 * from `IIdentityContext`; the response serialises as `{ subjectId, roles }` (camelCased anon
 * object), where each role is the backend `UserRole` name (`User` / `Admin`).
 *
 * Only what the domain cares about crosses the wire — the IdP `sub` and the mapped roles — never
 * raw claims (the swappable-identity seam, invariant #11: no extra PII).
 */

/** The application's own roles, mirroring the backend `UserRole` enum names. */
export type UserRole = 'User' | 'Admin';

/**
 * The `/me` identity shape. `subjectId` is the external IdP subject (`sub`); it is `null` only in
 * shapes the backend could theoretically return for an anonymous principal, though the endpoint
 * itself requires authentication.
 */
export interface IdentityDto {
  readonly subjectId: string | null;
  readonly roles: readonly UserRole[];
}
