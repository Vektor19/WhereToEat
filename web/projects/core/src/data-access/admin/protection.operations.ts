/**
 * Admin protection-flag operations (admin > parser, invariant #3): the per-menu-item
 * `do-not-parse` flag and the restaurant-level `do-not-update` flag that the parser's persist gate
 * observes so hand-curated data is never overwritten. One interface + `HttpClient` adapter per
 * endpoint. Both send the `{ value }` `FlagBody` and return **204 No Content** on success.
 *
 * Endpoints:
 *  - `PUT /admin/menu-items/{id}/do-not-parse`        → {@link SetMenuItemDoNotParseOperation}
 *  - `PUT /admin/restaurants/{id}/do-not-update`      → {@link SetRestaurantDoNotUpdateOperation}
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { FlagBody } from './admin-bodies';

/** `PUT /admin/menu-items/{id}/do-not-parse` — flag a menu item the parser must skip. */
export interface SetMenuItemDoNotParseOperation {
  execute(menuItemId: string, value: boolean): Observable<void>;
}

/** `PUT /admin/restaurants/{id}/do-not-update` — flag a restaurant the parser must not overwrite. */
export interface SetRestaurantDoNotUpdateOperation {
  execute(restaurantId: string, value: boolean): Observable<void>;
}

@Injectable({ providedIn: 'root' })
export class HttpSetMenuItemDoNotParseOperation implements SetMenuItemDoNotParseOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(menuItemId: string, value: boolean): Observable<void> {
    const body: FlagBody = { value };
    return this.http.put<void>(
      this.api.adminUrl(`/admin/menu-items/${menuItemId}/do-not-parse`),
      body,
    );
  }
}

@Injectable({ providedIn: 'root' })
export class HttpSetRestaurantDoNotUpdateOperation implements SetRestaurantDoNotUpdateOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string, value: boolean): Observable<void> {
    const body: FlagBody = { value };
    return this.http.put<void>(
      this.api.adminUrl(`/admin/restaurants/${restaurantId}/do-not-update`),
      body,
    );
  }
}
