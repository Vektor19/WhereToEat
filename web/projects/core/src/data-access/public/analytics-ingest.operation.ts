/**
 * The `POST /analytics/events` ingest operation (§8.1). One interface + `HttpClient` adapter.
 *
 * **Wire casing (load-bearing).** The ingest parser does `Enum.TryParse<EventKind>(kind,
 * ignoreCase: true)` and does NOT strip underscores — so the in-app snake_case `kind`
 * (`card_open` / `rating_given`) is serialized via {@link EVENT_KIND_WIRE_NAME} to the C#
 * enum-member spelling (`CardOpen` / `RatingGiven`); an unknown kind would 400 the whole batch.
 *
 * The batch body is `{ events: [...] }` and the happy path returns **202 Accepted** with no body.
 * Anonymous (clients report their own behaviour). The session id / coordinates ride here only as
 * far as the server-side anonymizer (invariant #11).
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { RawEventBody } from '../../domain';
import { EVENT_KIND_WIRE_NAME } from '../../domain';

/** `POST /analytics/events` — ingest a best-effort batch of raw analytics events (202 Accepted). */
export interface IngestAnalyticsOperation {
  execute(events: readonly RawEventBody[]): Observable<void>;
}

/** One raw event on the wire: identical to {@link RawEventBody} but with `kind` as the wire string. */
interface RawEventWire extends Omit<RawEventBody, 'kind'> {
  readonly kind: string;
}

/** The batch envelope the ingest endpoint binds (`IngestEventsRequestBody`). */
interface IngestEventsWire {
  readonly events: readonly RawEventWire[];
}

@Injectable({ providedIn: 'root' })
export class HttpIngestAnalyticsOperation implements IngestAnalyticsOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(events: readonly RawEventBody[]): Observable<void> {
    const body: IngestEventsWire = {
      events: events.map((e) => ({ ...e, kind: EVENT_KIND_WIRE_NAME[e.kind] })),
    };

    // 202 Accepted with no body — type the response as void; the interceptor handles failures.
    return this.http.post<void>(this.api.publicUrl('/analytics/events'), body);
  }
}
