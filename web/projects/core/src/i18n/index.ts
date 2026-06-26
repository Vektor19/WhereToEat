/**
 * Barrel for the `core` i18n layer (Step 17) — the **view-independent** localization seam: the locale
 * config (default/supported locales, persistence key), the bundled message catalogs, the in-memory
 * ngx-translate loader, and the {@link LocaleService} that drives the active locale (catalog +
 * `document.documentElement.lang` + persistence + the active-locale signal the formatters read).
 *
 * Ukrainian-first, localization-ready: adding a EU locale is a sibling catalog file plus a
 * {@link SUPPORTED_LOCALES} entry — no architectural change. Re-exported through the library
 * public-api so the `app` consumes it through the one boundary.
 */
export * from './locale.config';
export * from './translation-catalog';
export * from './in-memory-translate.loader';
export * from './locale.service';
export * from './translate-text';
