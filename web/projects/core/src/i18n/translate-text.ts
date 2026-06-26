/**
 * `translateText` — the single typed helper that resolves a **leaf-string** catalog key to a
 * `Signal<string>` (Step 17).
 *
 * ## Why this exists (the cast invariant, made explicit)
 * ngx-translate's reactive `TranslateService.translate(...)` is typed `Signal<Translation |
 * TranslationObject>` because a key *could* point at a nested sub-tree (an object of strings) rather
 * than a single leaf. In this app **every key passed to the translation API is a leaf-string catalog
 * entry** — the catalogs are flat dotted keys whose leaves are strings ({@link TranslationCatalog}),
 * and no view resolves a sub-tree. So the result is always a `string`, and casting to
 * `Signal<string>` is sound.
 *
 * Rather than repeat an inline `as Signal<string>` cast (and its justification) at every call site —
 * result-card, error-state, match-control, sort-control, restaurant-details, … — components call this
 * helper. The cast and the "catalog keys are leaf strings" invariant live in exactly one place, so the
 * assumption is documented once and a future catalog that violated it (a key pointing at a sub-tree)
 * has a single, obvious place to revisit.
 *
 * The signature mirrors `TranslateService.translate` (static or signal/arrow `key`/`params`, so the
 * returned signal re-resolves on a language switch and tracks reactive params), only narrowing the
 * return type to `Signal<string>`.
 */
import type { Signal } from '@angular/core';
import type { InterpolationParameters, TranslateService } from '@ngx-translate/core';

export function translateText(
  translate: TranslateService,
  key: string | string[] | (() => string | string[]),
  params?: InterpolationParameters | (() => InterpolationParameters | undefined),
): Signal<string> {
  // Safe because every catalog key in this app is a leaf string (see the doc comment above), so the
  // resolved value is always a `string`, never a `TranslationObject`.
  return translate.translate(key, params) as Signal<string>;
}
