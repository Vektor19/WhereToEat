/**
 * Analytics models — the TypeScript mirror of the backend ingest contract
 * (`POST /analytics/events`: `RawEventBody` items) and the `EventKind` enum
 * (`WhereToEat.Analytics.Domain.EventKind`).
 *
 * The raw event carries the only identifying fields (`userId`/`sessionId`,
 * `latitude`/`longitude`); the backend drops/hashes/coarsens them **before** persistence so they
 * never reach the database (invariant #11). The frontend stamps an opaque session id and the
 * opt-in approximate coordinates ride as optional `latitude`/`longitude` **fields** on the
 * relevant events — there is no `geo` event kind (see {@link EventKind}).
 */

/**
 * The in-app analytics event kinds — the canonical snake_case values, mirroring the backend
 * `EventKind` enum **exactly: its eight members and no others**.
 *
 * There is **no `geo` member** — the geolocation opt-in rides as optional `latitude`/`longitude`
 * fields on other events, never as its own kind. The backend ingest endpoint 400s the whole batch
 * on an unknown kind, so emitting a `geo` kind would reject every event in that batch.
 *
 * **Wire casing (load-bearing).** The ingest endpoint parses the wire `kind` with
 * `Enum.TryParse<EventKind>(kind, ignoreCase: true)` + `Enum.IsDefined`, which matches the **C#
 * member names** (`Impression`, `CardOpen`, `RatingGiven`, …) case-insensitively but does **not**
 * strip underscores — so a literal `card_open` / `rating_given` would 400. These union members are
 * the in-app values; the data layer (Steps 5/16) must serialise the wire `kind` using the
 * enum-member spelling via {@link EVENT_KIND_WIRE_NAME} so ingest never rejects a known kind.
 */
export type EventKind =
  | 'impression'
  | 'view'
  | 'card_open'
  | 'action'
  | 'search'
  | 'filter'
  | 'rating_given'
  | 'session';

/**
 * The complete set of in-app {@link EventKind} values, in the backend enum's declaration order
 * (`Impression = 0` … `Session = 7`). The single source the EventKind 1:1 test asserts against
 * and a runtime list the batching emitter (Step 16) can validate against.
 */
export const EVENT_KINDS: readonly EventKind[] = [
  'impression',
  'view',
  'card_open',
  'action',
  'search',
  'filter',
  'rating_given',
  'session',
] as const;

/**
 * Maps each in-app {@link EventKind} to the **wire `kind` string** the ingest parser accepts — the
 * C# enum-member spelling (PascalCase, no underscores). The parser is case-insensitive, so this
 * exact casing is sufficient and unambiguous. The data layer (Steps 5/16) serialises with this map
 * rather than sending the snake_case union value directly (which would 400 for `card_open` /
 * `rating_given`).
 */
export const EVENT_KIND_WIRE_NAME: Readonly<Record<EventKind, string>> = {
  impression: 'Impression',
  view: 'View',
  card_open: 'CardOpen',
  action: 'Action',
  search: 'Search',
  filter: 'Filter',
  rating_given: 'RatingGiven',
  session: 'Session',
} as const;

/**
 * One raw analytics event as it goes on the wire to `POST /analytics/events` (mirrors the
 * backend `RawEventBody`). Every field but `kind` is optional, matching the backend record's
 * nullable members. The builders (Step 8) produce these payloads, stamping the session id and
 * never including PII; `userId`/`sessionId` and `latitude`/`longitude` are dropped/hashed/
 * coarsened server-side (invariant #11).
 *
 * Note: `kind` here is the in-app {@link EventKind}; the data layer converts it to the wire
 * spelling via {@link EVENT_KIND_WIRE_NAME} when serialising.
 */
export interface RawEventBody {
  readonly kind: EventKind;
  readonly occurredAtUtc?: string | null;
  readonly restaurantId?: string | null;
  readonly categoryIds?: readonly string[] | null;
  readonly dishIds?: readonly string[] | null;
  readonly sortMode?: string | null;
  readonly filters?: readonly string[] | null;
  readonly position?: number | null;
  readonly userId?: string | null;
  readonly sessionId?: string | null;
  readonly latitude?: number | null;
  readonly longitude?: number | null;
}
