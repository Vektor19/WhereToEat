/**
 * The `POST /recommend` operation (CLAUDE.md §6). One interface + `HttpClient` adapter.
 *
 * The request is serialized to the backend's exact wire body
 * (`{ items: [{categoryId|dishId}], match, sort, filters: [{key, value}], userGeo: {latitude,
 * longitude} }`) so the engine accepts it field-for-field. The pre-pipeline builder/validation
 * (Step 8) feeds a well-formed {@link RecommendationRequest}; this adapter only maps it to the
 * wire shape and posts it. A validation 400 comes back as `{ error, message }` and is normalized
 * by the Step 7 interceptor. Recommendation is an anonymous public read (no bearer).
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { RecommendationRequest, RecommendationResultDto, SelectedItem } from '../../domain';
import { isDishSelection } from '../../domain';

/** `POST /recommend` — runs the pluggable ranking pipeline and returns the ordered result. */
export interface RecommendOperation {
  execute(request: RecommendationRequest): Observable<RecommendationResultDto>;
}

/** The wire shape of a single selected item — exactly one of `categoryId` / `dishId` (invariant #2). */
interface SelectedItemWire {
  readonly categoryId?: string;
  readonly dishId?: string;
}

/** The wire shape of `POST /recommend`'s body, mirroring the backend `RecommendRequestBody`. */
interface RecommendRequestWire {
  readonly items: readonly SelectedItemWire[];
  readonly match: string;
  readonly sort: string;
  readonly filters: readonly { readonly key: string; readonly value?: string | null }[];
  readonly userGeo?: { readonly latitude: number; readonly longitude: number };
}

/** Map one domain selection to its single-id wire form (never emits both ids). */
function toWireItem(item: SelectedItem): SelectedItemWire {
  return isDishSelection(item) ? { dishId: item.dishId } : { categoryId: item.categoryId };
}

@Injectable({ providedIn: 'root' })
export class HttpRecommendOperation implements RecommendOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(request: RecommendationRequest): Observable<RecommendationResultDto> {
    const body: RecommendRequestWire = {
      items: request.items.map(toWireItem),
      match: request.match,
      sort: request.sort,
      filters: request.filters.map((f) => ({ key: f.key, value: f.value ?? null })),
      ...(request.userGeo
        ? {
            userGeo: {
              latitude: request.userGeo.latitude,
              longitude: request.userGeo.longitude,
            },
          }
        : {}),
    };

    return this.http.post<RecommendationResultDto>(this.api.publicUrl('/recommend'), body);
  }
}
