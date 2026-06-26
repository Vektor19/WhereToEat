/**
 * Locale service (Step 17) — the single seam that owns the **active locale** for the whole app.
 *
 * It wraps ngx-translate's runtime `TranslateService` and adds the three things a real language switch
 * needs beyond catalog resolution:
 *   - it drives the active catalog (`translate.use(code)`), so every `| translate` re-resolves;
 *   - it keeps `document.documentElement.lang` in sync (accessibility / correct hyphenation);
 *   - it **persists** the choice in localStorage so it survives reload and sessions;
 *   - it exposes the active locale as a **signal** the locale-aware formatters (Step 8) read, so
 *     number / distance / currency formatting follows the switch with no extra wiring.
 *
 * Ukrainian-first: {@link DEFAULT_LOCALE} (`uk`) is the launch default and the runtime fallback, so a
 * key missing from a partially-translated locale resolves to the Ukrainian string rather than a raw
 * key. Adding a locale is config-only (a catalog + a {@link SUPPORTED_LOCALES} entry) — this service
 * needs no change.
 *
 * Framework-light intent: the locale-selection logic (validate → persist → set lang → activate) is the
 * RN-portable seam; only the `TranslateService`/`DOCUMENT` wiring is Angular-specific.
 */
import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal, type Signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import {
  DEFAULT_LOCALE,
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
  asSupportedLocale,
  type LocaleDescriptor,
} from './locale.config';

@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);

  /** The active locale code as a signal — the formatters (Step 8) read this so they follow the switch. */
  private readonly active = signal<string>(DEFAULT_LOCALE);

  /** Read-only active locale code (drives locale-aware formatting). */
  readonly activeLocale: Signal<string> = computed(() => this.active());

  /** The locales offered in the switch, in display order (autonym labels). */
  readonly locales: readonly LocaleDescriptor[] = SUPPORTED_LOCALES;

  /**
   * Initialise the i18n runtime once at bootstrap: register the fallback to {@link DEFAULT_LOCALE}
   * (so a missing key in a partial locale falls back to Ukrainian) and activate the **persisted**
   * choice, defaulting to `uk`. Idempotent enough to call once from an app initializer.
   */
  init(): void {
    this.translate.setFallbackLang(DEFAULT_LOCALE);
    const persisted = asSupportedLocale(this.readPersisted());
    this.use(persisted ?? DEFAULT_LOCALE);
  }

  /**
   * Switch to `code` when it is a supported locale: activate its catalog, set
   * `document.documentElement.lang`, update the active-locale signal (so formatters re-run), and
   * persist the choice. An unsupported code is ignored (the active locale stays unchanged) so a bad
   * value can never blank the UI.
   */
  use(code: string): void {
    const locale = asSupportedLocale(code);
    if (locale === undefined) {
      return;
    }
    this.translate.use(locale);
    this.document.documentElement.lang = locale;
    this.active.set(locale);
    this.persist(locale);
  }

  /** Read the persisted locale choice, tolerating an unavailable/throwing storage (private mode). */
  private readPersisted(): string | null {
    try {
      return this.document.defaultView?.localStorage.getItem(LOCALE_STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  /** Persist the locale choice, swallowing a storage failure (never break a switch on persistence). */
  private persist(locale: string): void {
    try {
      this.document.defaultView?.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
    } catch {
      // Storage unavailable (private mode / disabled): the switch still works for this session.
    }
  }
}
