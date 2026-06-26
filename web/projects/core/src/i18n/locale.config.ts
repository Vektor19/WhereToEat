/**
 * i18n locale configuration (Step 17) — the **view-independent** locale model shared by the whole
 * app. It lives in `core` (not the view layer) so the catalog/locale plumbing is the same
 * RN-portable business-logic seam the design calls for: the React Native rewrite reuses these
 * locale codes, the default-locale choice, and the persistence key with the same catalogs, swapping
 * only the framework wiring.
 *
 * **Ukrainian-first, localization-ready (design Goal 4 / Risk #7).** `uk` is the launch default and
 * the only fully-populated catalog; the type and the supported-locale list are structured so adding
 * a European locale is a **catalog file plus a list entry** — no architectural change. The scaffold
 * `en` placeholder exists to prove that additive property.
 */

/** The launch default locale — Ukrainian (design Goal 4). Every key resolves in this catalog. */
export const DEFAULT_LOCALE = 'uk';

/**
 * The locales the app can switch between. `uk` is fully populated (the launch locale); `en` is a
 * **scaffold placeholder** proving additive locales need no code change — adding a real locale means
 * shipping its catalog and appending an entry here, nothing more. The `label` is the autonym shown in
 * the language switch (a language is named in its own tongue, so the label is not itself translated).
 */
export interface LocaleDescriptor {
  /** The BCP-47 locale code (also the `Intl` locale and `document.documentElement.lang` value). */
  readonly code: string;
  /** The language's autonym, shown in the switch (e.g. "Українська", "English"). */
  readonly label: string;
}

/**
 * The supported locales in display order. Launch ships `uk` populated and `en` as the additive-proof
 * placeholder; a new EU locale is appended here once its catalog exists (config-only, invariant: no
 * architectural change).
 */
export const SUPPORTED_LOCALES: readonly LocaleDescriptor[] = [
  { code: 'uk', label: 'Українська' },
  { code: 'en', label: 'English' },
];

/** The set of supported locale codes, for validating a persisted/requested choice. */
export const SUPPORTED_LOCALE_CODES: readonly string[] = SUPPORTED_LOCALES.map((l) => l.code);

/** localStorage key the language choice persists under, so a switch survives reload/sessions. */
export const LOCALE_STORAGE_KEY = 'dp.locale';

/** Narrow an arbitrary string to a supported locale code, or `undefined` when it is not one. */
export function asSupportedLocale(code: string | null | undefined): string | undefined {
  return code != null && SUPPORTED_LOCALE_CODES.includes(code) ? code : undefined;
}
