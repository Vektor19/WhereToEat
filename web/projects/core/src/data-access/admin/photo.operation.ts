/**
 * Admin photo-permission operation (invariant #8). Toggling a real photo's permission gate; our
 * own generic category photos stay the default everywhere else. One interface + `HttpClient`
 * adapter. Sends the `{ value }` `FlagBody` and returns **204 No Content** on success.
 *
 * Endpoint: `PUT /admin/photos/{id}/permission` → {@link SetPhotoPermissionOperation}.
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { FlagBody } from './admin-bodies';

/** `PUT /admin/photos/{id}/permission` — toggle whether a real photo may be shown. */
export interface SetPhotoPermissionOperation {
  execute(photoId: string, value: boolean): Observable<void>;
}

@Injectable({ providedIn: 'root' })
export class HttpSetPhotoPermissionOperation implements SetPhotoPermissionOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(photoId: string, value: boolean): Observable<void> {
    const body: FlagBody = { value };
    return this.http.put<void>(this.api.adminUrl(`/admin/photos/${photoId}/permission`), body);
  }
}
