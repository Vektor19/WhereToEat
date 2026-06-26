/**
 * The authenticated rating-submit operation (Step 22). One interface + `HttpClient` adapter.
 *
 * Endpoint: `POST /restaurants/{restaurantId}/ratings` with body `{ score }` (1..5) →
 * {@link SubmitRatingOperation}. It is a **Public-host authenticated** call: the Step 6 bearer
 * interceptor attaches the user token because the path ends with the `/ratings` suffix on its
 * allowlist (the anonymous `GET /restaurants/{id}` details read, which has no `/ratings` suffix, stays
 * token-free). The backend maps the IdP `sub` → an opaque user ref (no PII — invariant #11), validates
 * the score (1..5; a 400 `{ error, message }` otherwise, normalized by the Step 7 interceptor), and
 * publishes `RatingGiven` so the cumulative all-time aggregate recomputes the smoothed value the
 * recommend/details paths display (invariant #6). Success is **204 No Content**.
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';

/** Body for `POST /restaurants/{restaurantId}/ratings` — the 1..5 score (validated server-side). */
export interface SubmitRatingBody {
  readonly score: number;
}

/** `POST /restaurants/{restaurantId}/ratings` — submit (or revise) the caller's 1..5 score. */
export interface SubmitRatingOperation {
  execute(restaurantId: string, score: number): Observable<void>;
}

@Injectable({ providedIn: 'root' })
export class HttpSubmitRatingOperation implements SubmitRatingOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string, score: number): Observable<void> {
    const body: SubmitRatingBody = { score };
    return this.http.post<void>(this.api.publicUrl(`/restaurants/${restaurantId}/ratings`), body);
  }
}
