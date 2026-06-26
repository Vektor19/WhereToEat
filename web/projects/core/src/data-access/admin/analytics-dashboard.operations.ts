/**
 * Admin-scoped §7.5 analytics-dashboard read operations (Step 24), wired to the Step 23 admin-host
 * read endpoints under `/admin/analytics/...`. One interface + `HttpClient` adapter per endpoint;
 * every URL is composed through {@link ApiConfig.adminUrl}, so the bearer interceptor (Step 6)
 * attaches the operator's token on the admin prefix and the Admin host enforces the admin policy
 * server-side.
 *
 * **Aggregates only (invariant #11 / §7.5).** Every response type below is a counts/ratios DTO
 * mirrored field-for-field from `IAnalyticsRollupReader`'s records — there is **no actor hash, no
 * per-event id, no precise coordinate, and no per-user row** in any shape here. The frontend cannot
 * receive or reconstruct per-user/per-event data because the seam never exposes it; a "selection id"
 * in the demand breakdown is a taxonomy item (invariant #2), never a person.
 *
 * Endpoints (all admin-gated: admin 200 / non-admin 403 / anonymous 401):
 *  - `GET /admin/analytics/restaurants/{id}/traffic?from=&to=`            → {@link GetTrafficOperation}
 *  - `GET /admin/analytics/restaurants/{id}/funnel?from=&to=`            → {@link GetFunnelOperation}
 *  - `GET /admin/analytics/demand?from=&to=&top=`                        → {@link GetDemandOperation}
 *  - `GET /admin/analytics/restaurants/{id}/price-positioning`           → {@link GetPricePositioningOperation}
 *  - `GET /admin/analytics/restaurants/{id}/ratings`                     → {@link GetRatingsDistributionOperation}
 */
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type {
  ConversionFunnelDto,
  DemandBreakdownDto,
  PricePositioningDto,
  RatingsDistributionDto,
  RestaurantTrafficSummaryDto,
} from '../../domain';

/** `GET /admin/analytics/restaurants/{id}/traffic` — visibility/traffic over a window. */
export interface GetTrafficOperation {
  execute(restaurantId: string, from: string, to: string): Observable<RestaurantTrafficSummaryDto>;
}

/** `GET /admin/analytics/restaurants/{id}/funnel` — impression → card-open → action funnel. */
export interface GetFunnelOperation {
  execute(restaurantId: string, from: string, to: string): Observable<ConversionFunnelDto>;
}

/** `GET /admin/analytics/demand` — most-searched categories/dishes in a window (area-wide). */
export interface GetDemandOperation {
  execute(from: string, to: string, top?: number): Observable<DemandBreakdownDto>;
}

/** `GET /admin/analytics/restaurants/{id}/price-positioning` — per-dish price vs market median. */
export interface GetPricePositioningOperation {
  execute(restaurantId: string): Observable<PricePositioningDto>;
}

/** `GET /admin/analytics/restaurants/{id}/ratings` — cumulative all-time ratings distribution. */
export interface GetRatingsDistributionOperation {
  execute(restaurantId: string): Observable<RatingsDistributionDto>;
}

/** The ISO-8601 `from`/`to` window both behavioural endpoints require (the host 400s if absent). */
function windowParams(from: string, to: string): HttpParams {
  return new HttpParams().set('from', from).set('to', to);
}

@Injectable({ providedIn: 'root' })
export class HttpGetTrafficOperation implements GetTrafficOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string, from: string, to: string): Observable<RestaurantTrafficSummaryDto> {
    return this.http.get<RestaurantTrafficSummaryDto>(
      this.api.adminUrl(`/admin/analytics/restaurants/${restaurantId}/traffic`),
      { params: windowParams(from, to) },
    );
  }
}

@Injectable({ providedIn: 'root' })
export class HttpGetFunnelOperation implements GetFunnelOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string, from: string, to: string): Observable<ConversionFunnelDto> {
    return this.http.get<ConversionFunnelDto>(
      this.api.adminUrl(`/admin/analytics/restaurants/${restaurantId}/funnel`),
      { params: windowParams(from, to) },
    );
  }
}

@Injectable({ providedIn: 'root' })
export class HttpGetDemandOperation implements GetDemandOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(from: string, to: string, top?: number): Observable<DemandBreakdownDto> {
    let params = windowParams(from, to);
    if (top !== undefined) {
      params = params.set('top', top);
    }
    return this.http.get<DemandBreakdownDto>(this.api.adminUrl('/admin/analytics/demand'), {
      params,
    });
  }
}

@Injectable({ providedIn: 'root' })
export class HttpGetPricePositioningOperation implements GetPricePositioningOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string): Observable<PricePositioningDto> {
    return this.http.get<PricePositioningDto>(
      this.api.adminUrl(`/admin/analytics/restaurants/${restaurantId}/price-positioning`),
    );
  }
}

@Injectable({ providedIn: 'root' })
export class HttpGetRatingsDistributionOperation implements GetRatingsDistributionOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string): Observable<RatingsDistributionDto> {
    return this.http.get<RatingsDistributionDto>(
      this.api.adminUrl(`/admin/analytics/restaurants/${restaurantId}/ratings`),
    );
  }
}
