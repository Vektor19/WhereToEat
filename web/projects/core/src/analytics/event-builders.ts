/**
 * Analytics event builders (Step 8). Produce the exact {@link RawEventBody} payloads for each of the
 * backend's **eight** `EventKind`s (impression / view / card_open / action / search / filter /
 * rating_given / session), stamping the per-session opaque id from the {@link SessionIdProvider}
 * (Step 7) onto every event and **never including PII** — the only identifying fields the wire
 * carries (`sessionId`, `latitude`/`longitude`) are dropped/hashed/coarsened server-side
 * (invariant #11).
 *
 * **No `geo` event kind exists** (the backend `EventKind` enum has none; sending one would 400 the
 * whole ingest batch). The opt-in approximate location therefore rides as optional `latitude`/
 * `longitude` **fields** on the relevant events (`search` / `session`) — never as its own kind.
 *
 * Builders emit the **in-app** snake_case {@link EventKind}; the data-access layer (Step 5) converts
 * `kind` to the wire spelling via {@link EVENT_KIND_WIRE_NAME} when serialising, so ingest never
 * rejects a known kind. These builders never call that map themselves — the conversion is the data
 * layer's responsibility (kept end-to-end so a builder cannot accidentally emit the wrong casing).
 *
 * Framework-light: an injectable service of pure builder methods, mirrored 1:1 by the RN rewrite.
 */
import { Injectable, inject } from '@angular/core';

import type { EventKind, RawEventBody } from '../domain';
import { SessionIdProvider } from './session-id.provider';

/** The opt-in approximate coordinates that ride as fields on `search`/`session` events (no `geo` kind). */
export interface EventGeo {
  readonly latitude: number;
  readonly longitude: number;
}

/** Inputs for an `impression` event — a card shown in the results list at `position`. */
export interface ImpressionEventInput {
  readonly restaurantId: string;
  readonly position: number;
}

/** Inputs for a `view` event — a result/details surface viewed for a restaurant. */
export interface ViewEventInput {
  readonly restaurantId: string;
  readonly position?: number;
}

/** Inputs for a `card_open` event — a result card opened. */
export interface CardOpenEventInput {
  readonly restaurantId: string;
  readonly position?: number;
}

/** Inputs for an `action` event — a target action (view-on-map / contact-link) on a restaurant. */
export interface ActionEventInput {
  readonly restaurantId: string;
}

/**
 * Inputs for a `search` event — the deterministic selection that built the query (category/dish ids,
 * sort mode) plus, when the user opted into geo, the approximate coordinates as fields.
 */
export interface SearchEventInput {
  readonly categoryIds?: readonly string[];
  readonly dishIds?: readonly string[];
  readonly sortMode?: string;
  readonly geo?: EventGeo;
}

/** Inputs for a `filter` event — the composable filter keys applied. */
export interface FilterEventInput {
  readonly filters: readonly string[];
  readonly sortMode?: string;
}

/** Inputs for a `rating_given` event — a rating submitted for a restaurant (Step 22 wires emission). */
export interface RatingGivenEventInput {
  readonly restaurantId: string;
}

/** Inputs for a `session` event — session start; carries geo as fields when the user opted in. */
export interface SessionEventInput {
  readonly geo?: EventGeo;
}

@Injectable({ providedIn: 'root' })
export class AnalyticsEventBuilders {
  private readonly sessionIds = inject(SessionIdProvider);

  /** Build an `impression` event for a shown result card at its list position. */
  impression(input: ImpressionEventInput): RawEventBody {
    return this.base('impression', {
      restaurantId: input.restaurantId,
      position: input.position,
    });
  }

  /** Build a `view` event for a viewed restaurant surface. */
  view(input: ViewEventInput): RawEventBody {
    return this.base('view', {
      restaurantId: input.restaurantId,
      position: input.position ?? null,
    });
  }

  /** Build a `card_open` event for an opened result card. */
  cardOpen(input: CardOpenEventInput): RawEventBody {
    return this.base('card_open', {
      restaurantId: input.restaurantId,
      position: input.position ?? null,
    });
  }

  /** Build an `action` event for a target action (view-on-map / contact-link click). */
  action(input: ActionEventInput): RawEventBody {
    return this.base('action', { restaurantId: input.restaurantId });
  }

  /**
   * Build a `search` event for a built selection. When `geo` is present (the user opted in) the
   * approximate `latitude`/`longitude` ride as **fields** here — there is no `geo` kind.
   */
  search(input: SearchEventInput): RawEventBody {
    return this.base('search', {
      categoryIds: this.nonEmpty(input.categoryIds),
      dishIds: this.nonEmpty(input.dishIds),
      sortMode: input.sortMode ?? null,
      ...this.geoFields(input.geo),
    });
  }

  /** Build a `filter` event for an applied set of composable filter keys. */
  filter(input: FilterEventInput): RawEventBody {
    return this.base('filter', {
      filters: this.nonEmpty(input.filters),
      sortMode: input.sortMode ?? null,
    });
  }

  /** Build a `rating_given` event for a submitted rating (Step 22 wires its emission). */
  ratingGiven(input: RatingGivenEventInput): RawEventBody {
    return this.base('rating_given', { restaurantId: input.restaurantId });
  }

  /**
   * Build a `session` event for session start. When `geo` is present the approximate coordinates
   * ride as **fields** (no `geo` kind).
   */
  session(input: SessionEventInput = {}): RawEventBody {
    return this.base('session', { ...this.geoFields(input.geo) });
  }

  /**
   * Stamp the common fields onto every event: the in-app `kind`, the client `occurredAtUtc`
   * timestamp, and the opaque session id (invariant #11 — no PII). `userId` is left absent here; the
   * authenticated rating flow (Step 22) does not stamp a user id on the client either — the backend
   * resolves identity from the bearer and anonymizes it.
   */
  private base(
    kind: EventKind,
    fields: Omit<Partial<RawEventBody>, 'kind' | 'occurredAtUtc' | 'sessionId'>,
  ): RawEventBody {
    return {
      kind,
      occurredAtUtc: new Date().toISOString(),
      sessionId: this.sessionIds.getSessionId(),
      ...fields,
    };
  }

  /** Spread the optional geo into `latitude`/`longitude` fields (absent when geo is off). */
  private geoFields(
    geo: EventGeo | undefined,
  ): Pick<Partial<RawEventBody>, 'latitude' | 'longitude'> {
    return geo === undefined ? {} : { latitude: geo.latitude, longitude: geo.longitude };
  }

  /** Return `undefined` for an empty/absent array so the field is omitted rather than sent empty. */
  private nonEmpty<T>(values: readonly T[] | undefined): readonly T[] | undefined {
    return values && values.length > 0 ? [...values] : undefined;
  }
}
