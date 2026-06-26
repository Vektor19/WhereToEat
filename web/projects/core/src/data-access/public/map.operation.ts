/**
 * The `GET /map/{restaurantId}` operation (§5.7 — "Глянути на карті"). One interface + `HttpClient`
 * adapter. Returns the live, never-cached payload assembled only from our stored fields (OSM
 * coordinates / Place ID / deep-link); `hasMapData=false` for a restaurant with no stored
 * coordinates, 404 for a missing restaurant (normalized by the Step 7 interceptor). Anonymous read.
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { MapPayloadDto } from '../../domain';

/** `GET /map/{restaurantId}` — the live map payload for the inline "view on map" affordance. */
export interface GetMapOperation {
  execute(restaurantId: string): Observable<MapPayloadDto>;
}

@Injectable({ providedIn: 'root' })
export class HttpGetMapOperation implements GetMapOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string): Observable<MapPayloadDto> {
    return this.http.get<MapPayloadDto>(this.api.publicUrl(`/map/${restaurantId}`));
  }
}
