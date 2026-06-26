/**
 * Admin monetization operations (Verified subscription + labeled ad placements, invariant #10 —
 * paid slots are always marked and separate from organic ranking). One interface + `HttpClient`
 * adapter per endpoint. Grant/revoke return **204 No Content**; ad-placement create returns
 * **201 Created** with `{ id }`.
 *
 * Endpoints:
 *  - `PUT    /admin/venues/{id}/verified`       → {@link GrantVerifiedOperation}
 *  - `DELETE /admin/venues/{id}/verified`       → {@link RevokeVerifiedOperation}
 *  - `POST   /admin/venues/{id}/ad-placements`  → {@link CreateAdPlacementOperation}
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type {
  CreateAdPlacementBody,
  CreatedAdPlacement,
  GrantVerifiedBody,
  VerifiedTier,
} from './admin-bodies';
import { VERIFIED_TIER_ORDINALS } from './admin-bodies';

/** `PUT /admin/venues/{id}/verified` — grant the chosen Verified tier (`Basic` / `Pro`). */
export interface GrantVerifiedOperation {
  execute(venueId: string, tier: VerifiedTier): Observable<void>;
}

/** `DELETE /admin/venues/{id}/verified` — revoke a venue's Verified status. */
export interface RevokeVerifiedOperation {
  execute(venueId: string): Observable<void>;
}

/** `POST /admin/venues/{id}/ad-placements` — create a targeted, time-bounded labeled slot. */
export interface CreateAdPlacementOperation {
  execute(venueId: string, body: CreateAdPlacementBody): Observable<CreatedAdPlacement>;
}

@Injectable({ providedIn: 'root' })
export class HttpGrantVerifiedOperation implements GrantVerifiedOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(venueId: string, tier: VerifiedTier): Observable<void> {
    const body: GrantVerifiedBody = { tier: VERIFIED_TIER_ORDINALS[tier] };
    return this.http.put<void>(this.api.adminUrl(`/admin/venues/${venueId}/verified`), body);
  }
}

@Injectable({ providedIn: 'root' })
export class HttpRevokeVerifiedOperation implements RevokeVerifiedOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(venueId: string): Observable<void> {
    return this.http.delete<void>(this.api.adminUrl(`/admin/venues/${venueId}/verified`));
  }
}

@Injectable({ providedIn: 'root' })
export class HttpCreateAdPlacementOperation implements CreateAdPlacementOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(venueId: string, body: CreateAdPlacementBody): Observable<CreatedAdPlacement> {
    return this.http.post<CreatedAdPlacement>(
      this.api.adminUrl(`/admin/venues/${venueId}/ad-placements`),
      body,
    );
  }
}
