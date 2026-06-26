/**
 * The `GET /health` operation. One interface + `HttpClient` adapter. The health-check endpoint
 * returns a short status string (ASP.NET `MapHealthChecks` plain-text body, e.g. `Healthy`); it is
 * read as text so a non-JSON body never triggers a parse error. Anonymous read.
 */
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiConfig } from '../../config/api-config';

/** `GET /health` — the Public host liveness probe (plain-text status). */
export interface GetHealthOperation {
  execute(): Observable<string>;
}

@Injectable({ providedIn: 'root' })
export class HttpGetHealthOperation implements GetHealthOperation {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiConfig);

  execute(): Observable<string> {
    return this.http.get(this.api.publicUrl('/health'), { responseType: 'text' });
  }
}
