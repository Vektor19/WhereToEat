/**
 * Global test providers for the `app` project's unit suite (Step 17).
 *
 * The `@angular/build:unit-test` builder injects this array into the test environment's root
 * `TestBed`, so every component/spec gets a **fully configured runtime i18n** without each spec
 * re-declaring it. This mirrors how the app is wired at runtime (`app.config.ts`): the in-memory
 * loader serves the bundled `core` catalogs and `uk` is both the active and the fallback locale, so
 * any `| translate` / `TranslateService` resolves the real Ukrainian copy in tests — which is exactly
 * what the component specs assert (translated text, not raw keys).
 *
 * Keeping this view-independent and catalog-driven means adding a EU locale needs no test change:
 * the same bundled catalogs flow in. Specs that need extra providers still add their own on top.
 *
 * Note: `@ngx-translate/core` is partially (AOT) compiled and its module-init needs the Angular JIT
 * compiler facade, which the unit-test environment does not load by default. The `test` build
 * configuration in `angular.json` adds `@angular/compiler` to the test polyfills so it loads before
 * ngx-translate evaluates — without it this providers file (and any spec importing a translated
 * component) throws "needs to be compiled using the JIT compiler".
 */
import { provideTranslateService, provideTranslateLoader } from '@ngx-translate/core';
import { DEFAULT_LOCALE, InMemoryTranslateLoader } from 'core';

const testProviders = [
  provideTranslateService({
    lang: DEFAULT_LOCALE,
    fallbackLang: DEFAULT_LOCALE,
    loader: provideTranslateLoader(InMemoryTranslateLoader),
  }),
];

export default testProviders;
