import { registerLocaleData } from '@angular/common';
import localeUk from '@angular/common/locales/uk';
import {
  ApplicationConfig,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  inject,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideOAuthClient } from 'angular-oauth2-oidc';
import { provideTranslateService, provideTranslateLoader } from '@ngx-translate/core';
import {
  APP_CONFIG,
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  AuthService,
  DEFAULT_LOCALE,
  GeoOptInStore,
  InMemoryTranslateLoader,
  LocaleService,
  SelectionStore,
  bearerInterceptor,
  errorNormalizationInterceptor,
  provideMapProvider,
} from 'core';

import { routes } from './app.routes';
import { environment } from '../environments/environment';

// Register Ukrainian CLI locale data so Angular's locale-aware pipes (number/date) and `LOCALE_ID`
// have the `uk` rules at launch (the `Intl`-based formatters in `core` need no registration, but
// `LOCALE_ID` consumers do). Adding a EU locale registers its data the same way — config-only.
registerLocaleData(localeUk);

/**
 * Emit the session-boundary analytics event once at app start (§5.9 / §8.1) through the single
 * batching emitter — best-effort and non-blocking, so analytics never delays or breaks bootstrap.
 * The opt-in approximate geo rides as `latitude`/`longitude` FIELDS (never a `geo` kind, invariant
 * #11) only when the user is already opted in and a coordinate is available; otherwise the fields
 * are omitted. Exported (not inlined) so the wiring is directly unit-testable. Runs in the
 * `provideAppInitializer` injection context, so it uses `inject()`.
 */
export function emitSessionStart(): void {
  const emitter = inject(AnalyticsEmitterService);
  const events = inject(AnalyticsEventBuilders);
  const optedIn = inject(GeoOptInStore).optedIn();
  const geo = optedIn ? inject(SelectionStore).userGeo() : undefined;
  emitter.emit(events.session(geo === undefined ? {} : { geo }));
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // `withComponentInputBinding` binds route params/query/data straight to component
    // `input()`s, so the restaurant-details route param `:id` flows into the details
    // container's `id` input with no manual ActivatedRoute plumbing (Step 14).
    provideRouter(routes, withComponentInputBinding()),
    // Angular Material components rely on the animations module; loaded async so
    // the initial bundle stays lean.
    provideAnimationsAsync(),
    // The data-access layer (Step 5) issues requests through Angular's HttpClient.
    // Interceptor order is load-bearing. On the request path the chain runs
    // outer→inner, so the bearer interceptor (Step 6) attaches the token before
    // the request goes out. On the response/error path the chain reverses
    // (inner→outer), so the error-normalization interceptor (Step 7) — registered
    // inner — sees failures first: its catchError normalizes every error into the
    // typed ApiError before it propagates back through the bearer interceptor (which
    // has no response-side logic) and on to feature code.
    provideHttpClient(withInterceptors([bearerInterceptor, errorNormalizationInterceptor])),
    // OIDC Authorization Code + PKCE client (angular-oauth2-oidc). AuthService
    // configures it from APP_CONFIG, so the Keycloak authority/client/redirect/
    // scopes stay in environment config, never hardcoded.
    provideOAuthClient(),
    // Supply the typed app configuration from the environment file. `core`
    // services (e.g. ApiConfig, AuthService) consume APP_CONFIG, so no host
    // string or credential is hardcoded in feature code; the prod build swaps in
    // environment.prod.ts via angular.json fileReplacements.
    { provide: APP_CONFIG, useValue: environment },
    // Bind the active map provider (§5.7, decision #11) behind MAP_PROVIDER: the
    // Google Embed provider when an Embed credential is configured, else the
    // deep-link degradation. The map modal injects the single port and never knows
    // which implementation backs it; the selection stays config-driven.
    provideMapProvider(),
    // i18n runtime catalogs (Step 17). `@ngx-translate/core` is configured with the
    // in-memory loader that serves the bundled `core` catalogs (no HTTP) and `uk` as
    // the launch + fallback locale, so every `| translate` resolves a key to the
    // Ukrainian copy and switching locale swaps catalogs at runtime (no rebuild). The
    // active locale defaults to `uk` here; the persisted choice is applied by
    // `LocaleService.init()` below. Adding a EU locale is a catalog file + a
    // SUPPORTED_LOCALES entry — no change to this wiring.
    provideTranslateService({
      lang: DEFAULT_LOCALE,
      fallbackLang: DEFAULT_LOCALE,
      loader: provideTranslateLoader(InMemoryTranslateLoader),
    }),
    // `LOCALE_ID` is PINNED to `uk` and does NOT follow the runtime language switch:
    // it is a static provider, so an Angular built-in locale-aware pipe (DatePipe/
    // DecimalPipe/CurrencyPipe) would always format in `uk` regardless of the active
    // locale. This is intentional and safe TODAY because the app does NOT use those
    // pipes for locale-sensitive output — all locale-aware formatting goes through the
    // `Intl`-based `core` formatters (currency/distance/number), which DO follow
    // `LocaleService.activeLocale()` and re-run on a switch. If an Angular locale-aware
    // pipe is added later, this provider must be made to follow the active locale (e.g.
    // a factory reading `LocaleService`) or its output will silently mismatch the UI.
    { provide: LOCALE_ID, useValue: DEFAULT_LOCALE },
    // Activate the i18n runtime (fallback + persisted locale → active catalog +
    // `document.documentElement.lang`) before the app renders, so the first paint is
    // already in the user's chosen language with no flash of untranslated keys.
    provideAppInitializer(() => inject(LocaleService).init()),
    // Load the OIDC discovery document and complete any in-flight redirect before
    // the app renders, so the session/role signals are populated up front and the
    // admin guard can decide synchronously.
    provideAppInitializer(() => inject(AuthService).init()),
    // Emit the session-boundary analytics event once at app start (§5.9 / §8.1) — see
    // `emitSessionStart` for the geo-fields-not-kind handling (invariant #11).
    provideAppInitializer(emitSessionStart),
  ],
};
