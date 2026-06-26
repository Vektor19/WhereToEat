/**
 * In-memory ngx-translate loader (Step 17). Serves the **bundled** catalogs straight from `core`
 * rather than fetching catalog JSON over HTTP.
 *
 * Why in-memory rather than the HTTP loader: the design puts the catalogs in the **view-independent
 * `core` layer** (so they are the RN-portable seam), the launch ships a single locale, and an
 * in-memory loader keeps the catalogs synchronously available — no network round-trip, no
 * HttpTestingController plumbing in every component test, and a switch is an instant, rebuild-free
 * runtime swap (the very reason the plan chose runtime catalogs over compile-time `$localize`).
 * Swapping to an HTTP/lazy loader later is a provider change only; nothing else depends on this class.
 */
import { Injectable } from '@angular/core';
import { TranslateLoader, type TranslationObject } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';

import { EN_CATALOG } from './catalogs/en.catalog';
import { UK_CATALOG } from './catalogs/uk.catalog';
import type { CatalogRegistry } from './translation-catalog';

/** The bundled catalogs keyed by locale code — `uk` populated, `en` the additive-proof placeholder. */
export const BUNDLED_CATALOGS: CatalogRegistry = {
  uk: UK_CATALOG,
  en: EN_CATALOG,
};

@Injectable()
export class InMemoryTranslateLoader extends TranslateLoader {
  /**
   * Resolve a locale's catalog synchronously (wrapped in `of` to satisfy the loader contract). An
   * unknown locale yields an empty catalog, so ngx-translate falls back to the configured fallback
   * locale (`uk`) rather than throwing.
   */
  override getTranslation(lang: string): Observable<TranslationObject> {
    return of((BUNDLED_CATALOGS[lang] ?? {}) as TranslationObject);
  }
}
