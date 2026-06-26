/**
 * Public deterministic prefix-search operations (invariant #1 — anchored prefix filtering, no
 * NLP). One interface + `HttpClient` adapter per endpoint; the literal `prefix` and `limit` ride
 * as query params (never a free-text body).
 *
 * Endpoints:
 *  - `GET /search/categories?prefix=&limit=` → {@link SearchCategoriesOperation}
 *  - `GET /search/dishes?prefix=&limit=`     → {@link SearchDishesOperation}
 */
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';
import type { CategoryDto, DishDto } from '../../domain';

/** `GET /search/categories` — anchored prefix search over categories. */
export interface SearchCategoriesOperation {
  execute(prefix: string, limit: number): Observable<readonly CategoryDto[]>;
}

/** `GET /search/dishes` — anchored prefix search over dishes. */
export interface SearchDishesOperation {
  execute(prefix: string, limit: number): Observable<readonly DishDto[]>;
}

/** Build the shared `prefix`/`limit` query params used by both search endpoints. */
function searchParams(prefix: string, limit: number): HttpParams {
  return new HttpParams().set('prefix', prefix).set('limit', limit);
}

@Injectable({ providedIn: 'root' })
export class HttpSearchCategoriesOperation implements SearchCategoriesOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(prefix: string, limit: number): Observable<readonly CategoryDto[]> {
    return this.http.get<readonly CategoryDto[]>(this.api.publicUrl('/search/categories'), {
      params: searchParams(prefix, limit),
    });
  }
}

@Injectable({ providedIn: 'root' })
export class HttpSearchDishesOperation implements SearchDishesOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(prefix: string, limit: number): Observable<readonly DishDto[]> {
    return this.http.get<readonly DishDto[]>(this.api.publicUrl('/search/dishes'), {
      params: searchParams(prefix, limit),
    });
  }
}
