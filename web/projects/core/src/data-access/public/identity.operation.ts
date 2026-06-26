/**
 * The authenticated `GET /me` operation (the identity-seam probe). One interface + `HttpClient`
 * adapter. This call requires a bearer token, but the token is attached by the Step 6 bearer
 * interceptor (matched by URL) — this adapter just issues the request. A 401 when unauthenticated
 * is normalized by the Step 7 interceptor.
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { IdentityDto } from '../../domain';

/** `GET /me` — echo the authenticated caller's mapped subject and roles. */
export interface GetMeOperation {
  execute(): Observable<IdentityDto>;
}

@Injectable({ providedIn: 'root' })
export class HttpGetMeOperation implements GetMeOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(): Observable<IdentityDto> {
    return this.http.get<IdentityDto>(this.api.publicUrl('/me'));
  }
}
