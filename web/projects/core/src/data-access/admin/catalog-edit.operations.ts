/**
 * Admin catalog-edit operations (admin > parser, invariant #3). One interface + `HttpClient`
 * adapter per endpoint; every URL is composed through {@link ApiConfig.adminUrl} so the Admin-host
 * prefix is never hardcoded. The bearer is attached by the Step 6 interceptor (matched by the admin
 * URL); these adapters just issue the request. Both return **204 No Content** on success.
 *
 * Endpoints:
 *  - `PUT /admin/menu-items/{id}`              → {@link EditMenuItemOperation}
 *  - `PUT /admin/restaurants/{id}/address`     → {@link EditAddressOperation}
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { EditAddressBody, EditMenuItemBody } from './admin-bodies';

/** `PUT /admin/menu-items/{id}` — edit a menu item's price/weight and re-categorise it. */
export interface EditMenuItemOperation {
  execute(menuItemId: string, body: EditMenuItemBody): Observable<void>;
}

/** `PUT /admin/restaurants/{id}/address` — edit a restaurant's address (triggers re-geocode). */
export interface EditAddressOperation {
  execute(restaurantId: string, body: EditAddressBody): Observable<void>;
}

@Injectable({ providedIn: 'root' })
export class HttpEditMenuItemOperation implements EditMenuItemOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(menuItemId: string, body: EditMenuItemBody): Observable<void> {
    return this.http.put<void>(this.api.adminUrl(`/admin/menu-items/${menuItemId}`), body);
  }
}

@Injectable({ providedIn: 'root' })
export class HttpEditAddressOperation implements EditAddressOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string, body: EditAddressBody): Observable<void> {
    return this.http.put<void>(
      this.api.adminUrl(`/admin/restaurants/${restaurantId}/address`),
      body,
    );
  }
}
