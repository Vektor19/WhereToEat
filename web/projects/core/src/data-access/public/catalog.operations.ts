/**
 * Public catalog read operations — one operation interface (port) per endpoint plus its
 * `HttpClient` adapter, so feature code depends on the interface, not on `HttpClient`
 * (dependency inversion). Every URL is composed through {@link ApiConfig.publicUrl} so the
 * Public-host prefix is never hardcoded.
 *
 * Endpoints:
 *  - `GET /categories`                  → {@link GetCategoriesOperation}
 *  - `GET /categories/{id}/dishes`      → {@link GetDishesByCategoryOperation}
 *  - `GET /restaurants/{id}`            → {@link GetRestaurantDetailsOperation} (404 → typed not-found)
 *
 * Reads stay anonymous (no bearer); errors propagate as raw `HttpErrorResponse` for the Step 7
 * interceptor to normalize onto the {@link ApiError} model.
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { CategoryDto, DishDto, RestaurantDetailsDto } from '../../domain';

/** `GET /categories` — list the taxonomy's top level (the categories the user picks from). */
export interface GetCategoriesOperation {
  execute(): Observable<readonly CategoryDto[]>;
}

/** `GET /categories/{id}/dishes` — list the dishes within a category (the lower taxonomy level). */
export interface GetDishesByCategoryOperation {
  execute(categoryId: string): Observable<readonly DishDto[]>;
}

/**
 * `GET /restaurants/{id}` — full restaurant details. A missing restaurant returns 404, which the
 * Step 7 interceptor maps to the {@link ApiError} `not-found` kind; this operation just calls it.
 */
export interface GetRestaurantDetailsOperation {
  execute(restaurantId: string): Observable<RestaurantDetailsDto>;
}

@Injectable({ providedIn: 'root' })
export class HttpGetCategoriesOperation implements GetCategoriesOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(): Observable<readonly CategoryDto[]> {
    return this.http.get<readonly CategoryDto[]>(this.api.publicUrl('/categories'));
  }
}

@Injectable({ providedIn: 'root' })
export class HttpGetDishesByCategoryOperation implements GetDishesByCategoryOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(categoryId: string): Observable<readonly DishDto[]> {
    return this.http.get<readonly DishDto[]>(
      this.api.publicUrl(`/categories/${categoryId}/dishes`),
    );
  }
}

@Injectable({ providedIn: 'root' })
export class HttpGetRestaurantDetailsOperation implements GetRestaurantDetailsOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(restaurantId: string): Observable<RestaurantDetailsDto> {
    return this.http.get<RestaurantDetailsDto>(this.api.publicUrl(`/restaurants/${restaurantId}`));
  }
}
