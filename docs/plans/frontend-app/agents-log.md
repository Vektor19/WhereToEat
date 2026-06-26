# Agents Log — frontend-app

_Append-only debug log. Agents never read this file._

---
- **time:** 2026-06-18T14:14:45Z
- **agent:** developer
- **status:** completed
- **message:** Environment readiness check: Node 22.20.0, npm 10.9.3, yarn 1.22.22 (no pnpm), Angular CLI 21.2.16 via npx, .NET SDK 8.0.417 (matches global.json pin; net8.0 target). Chrome present for Karma. Backend src/WhereToEat.sln builds green (0 warnings/errors); a transient file-lock CS2012 cleared after build-server shutdown. No web/ workspace yet (expected).

---
- **time:** 2026-06-18T14:15:39Z
- **agent:** qa
- **status:** completed
- **message:** Environment readiness check: Node 22 + npm 10 + Angular CLI 21 (npx) present; .NET 8 SDK + dotnet test green (all unit tests pass); Chrome installed at default path for Karma headless; Playwright not installed (no browser cache) - must install + run npx playwright install before Step 26 e2e; Docker 28 + compose v2 available for full-stack integration tests; web/ workspace does not exist yet (expected - Step 1 creates it); no blocking gaps for Steps 1-25 unit/component work.

---
- **time:** 2026-06-18T14:35:24Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 1: scaffolded web/ Angular 21 workspace (app + core library, projects/ root). Vitest unit runner, Angular ESLint + eslint-plugin-boundaries@5 import-boundary rules (consumer<->portal + core-barrel-only) verified to fail lint on a deliberate cross-boundary import, Prettier, Playwright (empty e2e). strict+strictTemplates on, OnPush root shell, lazy consumer/portal routes. build/test/lint/format all exit 0; dev server serves empty shell HTTP 200.

---
- **time:** 2026-06-18T14:36:23Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 1 verification: build OK; test OK (core 1, app 2 passing); lint OK; format OK; boundary lint proven to reject deliberate consumer->portal and deep-core imports (exit 1). Step left [~] in-progress for qa.

---
- **time:** 2026-06-18T14:46:06Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 1 review: all acceptance criteria met. Build/test/lint/format pass. Boundary enforcement verified (consumer->portal, deep-core imports all caught). strict+strictTemplates on, OnPush on root, lazy routes, Vitest wired, Playwright placeholder. One low finding: app.spec.ts missing provideRouter([]) - RouterOutlet renders without it in Angular 21 (test passes) but technically an incomplete test setup. One low finding: no-restricted-imports does not enumerate the tsconfig-resolved dist/core path in its patterns, relying purely on path string matching. Both are acceptable at this scaffold stage.

---
- **time:** 2026-06-18T14:50:12Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 1 revision: added provideRouter([]) to app.spec TestBed; repointed tsconfig 'core' path alias from ./dist/core to ./projects/core/src/public-api.ts so the boundaries classifier evaluates consumer->core. Re-proved probes (consumer->portal + deep-core-path fail; consumer->core barrel now classified/allowed). build core+app green, tests 1+2 pass, lint+format clean.

---
- **time:** 2026-06-18T14:54:12Z
- **agent:** qa
- **status:** completed
- **message:** Step 1 verified: build (consumer+portal lazy chunks), tests (3 pass via Vitest), lint clean, boundary rules confirmed live (consumer->portal blocked; deep-core blocked; barrel 'core' allowed), format clean, no src/ changes, no forward-step code.

---
- **time:** 2026-06-18T15:02:13Z
- **agent:** developer
- **status:** completed
- **message:** Step 2: design-token layer (_tokens.scss) + token-derived Material 3 theme (_theme.scss via mat.theme + mat.theme-overrides) + global styles (focus-visible, reduced-motion, sr-only); responsive mobile-first app-shell with topbar+lang-switch slot and global footer hosting the single discreet data-accuracy notice; WCAG-AA contrast unit check. Installed @angular/material,@angular/cdk,@angular/animations@^21.2. build+test(13 pass)+lint+format all green.

---
- **time:** 2026-06-18T15:08:10Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 2 reviewed: found 4 issues - (1) secondary/tertiary/error container colors not overridden in mat.theme-overrides breaking token-derivation for selected chips/form fields; (2) index.html lang=en for Ukrainian-first app violating WCAG 3.1.1; (3) raw breakpoints 600px/1024px and 1px border not tokenized; (4) raw -3rem in skip link.

---
- **time:** 2026-06-18T15:13:24Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 2 revision: re-pointed all 6 M3 container roles to tokens (added accent/error container tokens + palette steps), index.html lang=uk, tokenized breakpoints (Sass vars) + border-width + skip-link offset, added focus-ring/surface 3:1 contrast assertion. build/test(15)/lint/format green.

---
- **time:** 2026-06-18T15:18:07Z
- **agent:** qa
- **status:** completed
- **message:** Step 2 verified: build+14 tests green, lint+format clean. Token-derived theme confirmed (no raw hex outside _tokens.scss). Container roles (secondary/tertiary/error containers) all re-pointed to --dp-* tokens. Body-text contrast 5.88-16.61:1 (>= 4.5 AA), focus-ring 6.25:1 (>= 3:1). Shell: skip-link, focus-visible, reduced-motion, sr-only, lang=uk all present. Footer has exactly one data-accuracy notice; no 'updated X days ago'. Breakpoints as Sass vars (media-query compatible). Budget WARNING on app-shell styles is pre-existing, non-blocking. No src/ backend files touched.

---
- **time:** 2026-06-18T15:23:41Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 3: env config (environment.ts/.prod.ts), AppConfig token + ApiConfig in core, dev proxy.conf.json (/api/public->8080, /api/admin->8081, prefix stripped), angular.json serve proxyConfig + prod fileReplacements, APP_CONFIG provided from environment. core 8 tests pass (7 new ApiConfig), app 14 pass, build OK, lint OK, format OK.

---
- **time:** 2026-06-18T15:28:47Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 3 review: double-prefix model is consistent;  and // keys in proxy.conf.json are spurious Vite proxy entries; test coverage gap for admin URL cross-origin check

---
- **time:** 2026-06-18T15:32:21Z
- **agent:** developer
- **status:** completed
- **message:** Step 3 review fixes: switched proxy.conf.json to array-form (context field) removing spurious $schema/// keys, moved note to proxy.conf.md; added explicit toBe('/api/admin/admin/venues/1/verified') assertion in api-config.spec. Loader-verified 2 contexts; test 22 pass, lint/format/build green.

---
- **time:** 2026-06-18T15:37:13Z
- **agent:** qa
- **status:** completed
- **message:** Step 3: verified build (ng build app --configuration=production), 14 unit tests green (8 in core including all 7 ApiConfig assertions, 14 total in app), lint clean, format clean. Proxy config: valid array form, 2 entries only (/api/admin->8081, /api/public->8080, prefix-stripping, changeOrigin). Environment files carry empty gatewayOrigin (same-origin). fileReplacements confirmed in angular.json. APP_CONFIG provided from environment in app.config.ts. No cross-origin host in feature code. No auth/map/data-access pulled forward. No backend src/ files modified by Step 3.

---
- **time:** 2026-06-18T19:28:47Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 4: domain models, contract-key unions, EventKind union (8 members, no geo, wire-name map), SelectedItem discriminated union, api-error + identity. core build/lint/format green; 36 core tests pass (incl. EventKind 1:1 and both/neither-id compile-time guards). qa to mark done.

---
- **time:** 2026-06-18T19:34:36Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 4 domain models review: all 28 tests pass, fidelity to backend DTOs confirmed, EventKind 8-member bijection correct, no 'geo' kind, wire-casing map present, SelectedItem discriminated union enforced with @ts-expect-error compile guards, no any types, barrel exports complete

---
- **time:** 2026-06-18T19:37:42Z
- **agent:** qa
- **status:** completed
- **message:** Step 4: All acceptance criteria verified. Build green (ng build core + app), 36 core + 14 app tests pass, lint clean. EventKind union has exactly 8 members (no geo), wire-name map present, @ts-expect-error SelectedItem guards compile, no any in domain types, no backend src/ files modified, IDs are strings, money is amount+currency.

---
- **time:** 2026-06-18T19:46:52Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 5: typed data-access layer — one interface (port) + HttpClient adapter + injection token per endpoint (10 public, 8 admin); recommend wire-shape serialization, analytics kind via EVENT_KIND_WIRE_NAME, health as text. Added provideHttpClient to app.config. 22 new HttpTestingController tests; core 58 pass, app 14 pass, lint+format clean, build green.

---
- **time:** 2026-06-18T19:52:25Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 5: VerifiedTier enum serialization bug — sends string 'Basic'/'Pro' but backend expects integer (no JsonStringEnumConverter registered); test does not catch the mismatch

---
- **time:** 2026-06-18T19:56:01Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 5 revision: fixed verified-tier wire format — send SubscriptionTier integer ordinal (Basic=1, Pro=2) instead of member name; added VERIFIED_TIER_ORDINALS map + corrected comment; parametrized grantVerified test for Basic/Pro. core: 59 tests pass, build/lint/format green.

---
- **time:** 2026-06-18T19:59:59Z
- **agent:** qa
- **status:** completed
- **message:** Step 5: all 18 endpoint operations verified (10 public + 8 admin), URLs correct, test suite green (59 core + 14 app tests), build passes, lint passes, no any, no backend src/ changes

---
- **time:** 2026-06-18T20:08:29Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 6: Keycloak OIDC (Auth Code+PKCE) auth foundation — AuthService (signals: isAuthenticated/subject/roles/isAdmin) over angular-oauth2-oidc, bearer interceptor (admin host + authed-public allowlist /me,/ratings,/analytics/dashboard; no token on anonymous reads or cross-origin), adminGuard, role/JWT parsers (realm/client/flat claims). Wired provideOAuthClient + withInterceptors([bearerInterceptor]) + provideAppInitializer in app.config.ts. core 93/93 + app 14/14 tests pass; build/lint/format green.

---
- **time:** 2026-06-18T20:13:22Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 6 auth review: 3 issues found — APP_INITIALIZER does not catch Keycloak-down rejection (high), AUTHENTICATED_PUBLIC_PATHS includes /analytics/dashboard which is Admin-host not Public-host (medium), silent-refresh does not re-sync signals (medium)

---
- **time:** 2026-06-18T20:17:25Z
- **agent:** developer
- **status:** completed
- **message:** Step 6 revision: init() swallows discovery failures (boots unauthenticated when Keycloak down) + subscribes to OAuth events (token_received/refreshed/expires/logout) to re-sync signals; removed /analytics/dashboard from Public allowlist (Admin-host) + corrected doc-comments. Added 2 auth.service tests. core 95 passed, app 14 passed, lint clean, prettier clean, builds green.

---
- **time:** 2026-06-18T20:22:34Z
- **agent:** qa
- **status:** completed
- **message:** Step 6 verified: build passes (ng build app), 95/95 unit tests green (core), 14/14 app tests green, lint clean. OIDC config from APP_CONFIG only (responseType:code confirmed). Bearer interceptor: attaches to /api/admin/* and /api/public/me + /api/public/ratings; absent on all anonymous public paths, non-API paths, cross-origin URLs, and when no token. Resilient init: discovery-error test present and passing. Role-claims parser: realm_access/resource_access/flat role|roles, case-insensitive, mirrors backend RoleClaimMapper. adminGuard: admits admin, denies user-only, triggers login for anonymous. Signal re-sync on token_received event tested. JWT decode is client-side read-only. No , no backend src/ changes, auth in core only.

---
- **time:** 2026-06-18T20:30:42Z
- **agent:** developer
- **status:** completed
- **message:** Step 7: error-normalization interceptor (typed ApiError + i18n messageKey), LoadingService/RequestState async-state primitive, shared loading/error-state UI components, analytics SessionIdProvider; wired interceptor order in app.config (bearer then error). build+lint+format green; core 120/120, app 18/18.

---
- **time:** 2026-06-20T13:28:07Z
- **agent:** developer
- **status:** completed
- **message:** Step 7 reconciled: error-normalization interceptor (+spec), LoadingService/RequestState async-state primitive (+spec), SessionIdProvider (+spec), shared loading/error-state UI components (+error-state spec), all three core barrels + public-api exports, and app.config.ts interceptor order [bearer, errorNormalization] all present and coherent. No duplicates/dangling code. core 120/120, app 18/18 tests pass; lint clean; prettier clean; build OK (only a pre-existing Step-2 app-shell CSS budget warning).

---
- **time:** 2026-06-20T13:32:32Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 7 review: 5 issues found — wrong interceptor-ordering comment (docs), missing complete handler in RequestState.run() causing stuck loading on empty stream, unsafe 'as HttpErrorResponse' cast in interceptor, missing RequestState.destroy() for in-flight subscription cleanup, and typed error callback in run() using ApiError instead of unknown.

---
- **time:** 2026-06-20T13:37:45Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 7 reviewer fixes: RequestState.run() complete handler (no stuck loading on empty streams) + destroy() cleanup + unknown-typed error param with isApiError/asApiError wrapping; inverted interceptor-ordering comments rewritten (inner sees response first) in error.interceptor.ts and app.config.ts; guarded HttpErrorResponse instanceof cast in interceptor. Added specs. lint clean, format clean, core 125/125, app 18/18, build OK.

---
- **time:** 2026-06-20T13:41:07Z
- **agent:** qa
- **status:** completed
- **message:** Step 7 verified: 125 core tests + 18 app tests all pass; build clean (Step 2 SCSS budget warning only); lint clean; error interceptor maps all 7 kinds correctly with i18n keys; loading service transitions idle→loading→loaded/failed, EMPTY lands on loaded/empty, destroy() cancels in-flight; session-id stable/opaque/graceful-fallback; no any, no locale strings in core, no backend src/ changes, interceptor order correct.

---
- **time:** 2026-06-20T13:52:24Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 8: implemented core business-logic services (recommend request builder, validation, currency/distance/rating formatters, analytics event builders, Haversine display) + high-coverage unit specs. build/test/lint/format green; core 211 tests pass, app 18 tests pass.

---
- **time:** 2026-06-20T13:58:21Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 8 review: two issues found — tryBuild inverts build/validate order (medium), base() fields param allows kind override (low)

---
- **time:** 2026-06-20T14:00:59Z
- **agent:** developer
- **status:** completed
- **message:** Step 8 review fixes: corrected build() JSDoc precondition (drops false 'already validated' claim, names tryBuild as safe entry point); narrowed base() fields param to Omit<Partial<RawEventBody>,'kind'|'occurredAtUtc'|'sessionId'> and geoFields return to Pick<...,'latitude'|'longitude'> so stamped fields cannot be overridden. build 211+18 tests pass, lint clean, format clean.

---
- **time:** 2026-06-20T14:04:19Z
- **agent:** qa
- **status:** completed
- **message:** Step 8: All acceptance criteria verified. Build passes, 22 test files / 211 tests green, lint+format clean. Request builder produces correct wire shape; userGeo omitted when off; validation rejects empty selection, both/neither id items, out-of-range geo, unknown keys with typed results. CurrencyFormatter uses Intl by currency code (no hardcoded symbol). DistanceFormatter emits km. RatingPresenter emits LOW_REVIEW_NOTE_KEY below threshold=10 (aligned to backend C=10), no Google reference. Analytics event builders produce correct RawEventBody per all 8 EventKinds; geo carried as latitude/longitude fields on search/session, never as geo kind; base() uses Omit to prevent stamped field override; no userId stamped. EARTH_RADIUS_KM=6371.0088 matches backend. No any types. No backend src/ changes.

---
- **time:** 2026-06-20T14:11:59Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 9: implemented signal-store base + catalog/selection/results stores in core/state with barrels and public-api export; 4 new stores, 4 new spec files. core 269 tests pass, app 18 pass, lint/format/build green.

---
- **time:** 2026-06-20T14:16:26Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 9 reviewed: 2 issues found — (1) CatalogStore and ResultsStore do not extend SignalStore base (convention violation: preamble requires base applied consistently to every store), (2) missing test asserting clearGeo -> buildRequest omits userGeo key from wire body

---
- **time:** 2026-06-20T14:22:22Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 9 revision: CatalogStore+ResultsStore now extend SignalStore (RequestState held in base-managed state); added set->clear->build geo-omission test + dishes failed-load retry test. core 271 + app 18 tests pass, lint/format/build green.

---
- **time:** 2026-06-20T14:25:36Z
- **agent:** qa
- **status:** completed
- **message:** Step 9 verified: all 3 stores extend SignalStore; 271 core + 18 app tests green; build+lint+format clean; invariant #5/#11 assertions confirmed; no any, no backend src/ changes

---
- **time:** 2026-06-20T17:31:45Z
- **agent:** developer
- **status:** completed
- **message:** Step 10 reconciled: cleaned selection-builder container (top-level imports, removed dead code, fixed NG0600 signal-write-in-computed via dish-name registry), registered lazy selection route, added shared element-type to import boundaries, fixed prefix-search option a11y, added 4 specs. build OK; core 271 + app 45 tests pass; lint+format clean.

---
- **time:** 2026-06-20T17:39:28Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 10 review: 4 findings — 1 medium (residual NG0600-possible pattern in openDishes* computeds), 3 low (missing limit unit test, stale activeIndex ARIA, SelectionChip weak type coupling)

---
- **time:** 2026-06-20T17:46:49Z
- **agent:** developer
- **status:** completed
- **message:** Step 10 review fixes: (1) CatalogStore dishes read accessors no longer create entries (no-write sentinel, NG0600-safe) + tests; (2) SEARCH_LIMIT=10 cap test; (3) prefix-search resets active on results change + test; (4) SelectionChip discriminated union. Build OK; core 273 + app 47 tests pass; lint+format clean.

---
- **time:** 2026-06-20T17:54:21Z
- **agent:** qa
- **status:** completed
- **message:** Step 10 verified: build green, ng test core 273/273 + ng test app 47/47 (29 new Step-10 tests), lint+format clean, lazy-load confirmed in bundle output, NG0600-safe no-write accessor test present, boundaries config verified (shared edge allows consumer+portal→shared but not consumer↔portal), no any, all AC met

---
- **time:** 2026-06-20T18:04:27Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 11: match/sort/filters controls + recommend submit. New query-controls feature (match-control, sort-control with explicit-sort-dominates helper text, data-driven filters-control, smart query-controls container building+validating via SelectionStore.buildRequest and submitting through ResultsStore). Wired into selection-builder. app 65/65, core 273/273 green; lint+format clean; build ok.

---
- **time:** 2026-06-20T18:09:59Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 11 review: found one real issue (duplicate track key in filters-control test), one low-severity template gap (kind field not branched), rest clean


---
- **time:** 2026-06-20T18:14:00Z
- **agent:** developer
- **status:** completed
- **message:** Step 11 reviewer fixes. Issue 1 (MEDIUM): filters-control.component.spec.ts "renders a new filter key" test reused key 'rating' (already in DEFAULT_FILTER_DESCRIPTORS) causing a duplicate `track descriptor.key` value; changed to a distinct `'open-now' as FilterKey` so the test honestly proves data-driven rendering of a new entry. Issue 2 (LOW): made the filter-kind extensibility seam honest (option a) — added named `FilterKind` union, FilterDescriptor.kind now typed as FilterKind, and the template dispatches via `@if (descriptor.kind === 'numeric')` with a comment marking where a future non-numeric kind adds its branch; JSDoc updated to match. build OK (core+app), test OK (core 273/273, app 65/65), lint OK (app+core clean), format OK. Pre-existing app-shell SCSS budget warning (262 B over) unrelated. Step left [~] in-progress for qa.
---
- **time:** 2026-06-20T18:19:07Z
- **agent:** qa
- **status:** completed
- **message:** Step 11 verified: build green, 273 core + 65 app tests green (18 new Step-11 tests across 4 spec files), lint clean, Prettier clean. All 5 AC checked: match Or/And+combo hint; sort 5 modes+explicit-dominance hint+composite explained; filters data-driven+additive+clear; submit fail-fast+typed-errors+disabled-on-empty+loading; analytics filter event via INGEST_ANALYTICS seam. No any, no portal imports, no Step 12+ UI pulled forward. Pre-existing Step 2 SCSS budget warning non-blocking.

---
- **time:** 2026-06-20T18:30:15Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 12: consumer results list — backend-order cards (basket price by currency, smoothed rating + low-review note, optional distance, secondary coverage), generic-photo (invariant #8), labeled+separated ad-slot contract (invariant #10), CDK virtual scroll, impression/card_open emission. build+lint+format green; core 273, app 87 tests pass (22 new).

---
- **time:** 2026-06-21T06:57:10Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 12 review: found 4 issues — raw px values in result-card styles (96px/72px image column, max-width:600px media breakpoint should use Sass token), spec fixture hardcodes noteKey literal string instead of importing LOW_REVIEW_NOTE_KEY constant, missing test for hasRating:false/NO_REVIEWS_KEY branch in result-card spec, and missing test for virtual scroll threshold behavior (VIRTUALIZE_THRESHOLD) in results-list spec.

---
- **time:** 2026-06-21T07:01:29Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 12 reviewer cleanup: tokenized result-card thumb sizes (--dp-card-thumb-size[-sm]) + breakpoint via @use 'tokens'; spec uses LOW_REVIEW_NOTE_KEY/NO_REVIEWS_KEY constants; added no-reviews dash/note test and virtual-scroll threshold tests (21>thr present, 20 absent). app tests 90 passed, lint clean, build OK (pre-existing 4kB component-style warning only).

---
- **time:** 2026-06-21T07:05:28Z
- **agent:** qa
- **status:** completed
- **message:** Step 12 verified: 90 app tests + 273 core tests all green; build succeeds (2 anyComponentStyle warnings — non-blocking, flagged for Step 25); lint and Prettier pass; all acceptance criteria confirmed against real test runs

---
- **time:** 2026-06-21T07:19:50Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 13: inline View-on-map via swappable MAP_PROVIDER interface (core/map: provider port+token, google-embed + deep-link providers, config-driven selection/provideMapProvider); MapModalComponent (live Embed iframe sandboxed, deep-link degradation, no-map state, ARIA dialog + CDK focus-trap + ESC + focus return, action analytics event); result-card viewMap affordance + ad-slot/results-list wiring; renamed config MapProvider->MapProviderKind; added --dp-color-scrim token. Build/test/lint/format green: core 288, app 102 tests pass.

---
- **time:** 2026-06-21T07:26:14Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 13 review: 3 findings — (1) iframe sandbox allow-scripts+allow-same-origin anti-pattern needs documentation comment (no functional fix needed for the embed use case but the comment currently misrepresents it as safe); (2) dead code: dialogTitle viewChild declared but never used; (3) missing test: no spec verifies that reload() does NOT re-emit the action event.


---
- **time:** 2026-06-21T07:30:00Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 13 reviewer fixes. (1) Rewrote map-modal iframe-safety JSDoc to name the `allow-scripts allow-same-origin` sandbox-escape anti-pattern, state why it is accepted (Maps Embed requires both flags; URL is app-built config-keyed Google URL, never user input), and warn not to copy for untrusted origins. (2) Removed dead-end inconsistency: dropped `cdkTrapFocusAutoCapture`, now focus the dialog title via `afterNextRender(() => dialogTitle()?.nativeElement?.focus())` (keeps `tabindex="-1"`), so the viewChild is read and ARIA announces the dialog; CDK trap still keeps focus inside. (3) Added spec: after a payload + 1 `action` event, `reload()` + a second payload keeps `action` count at 1 (actionEmitted guard). lint OK (app+core), format OK, build app OK (only pre-existing result-card/app-shell budget warnings, unrelated), app tests 103 passed / 16 files. Left [~] in-progress for qa.
---
- **time:** 2026-06-21T07:35:59Z
- **agent:** qa
- **status:** completed
- **message:** Step 13 verified: all 391 unit tests pass (29 core + 16 app suites, 288+103 tests), lint clean, build succeeds (2 pre-existing budget warnings on app-shell/result-card unrelated to Step 13). MapProvider interface + MAP_PROVIDER token in core; Embed key from APP_CONFIG; selectMapProvider/provideMapProvider correct; no Google specifics in consumer modal; live fetch via GET_MAP, nothing cached, no Google rating/coords stored; hasMapData=false -> none state; iframe sandbox+referrerpolicy+sanitized src; inline open from card without navigation; ARIA dialog/aria-modal, CDK focus trap, ESC+backdrop close, focus returns to invoker, title focused on open; exactly one action event per open, not re-emitted on reload (test present); map in core barrel, modal in consumer tree, no portal imports, no any, no Step 14/15 pull-forward.

---
- **time:** 2026-06-21T07:51:50Z
- **agent:** developer
- **status:** completed
- **message:** Step 14: consumer restaurant-details page (route /restaurant/:id) — always-free contact links, menu with currency-by-priceCurrency + generic photos, 404 not-found/loading/error, view/card_open + action analytics; results card-open navigates here. core 291 + app 124 tests pass; lint+format clean; build green.

---
- **time:** 2026-06-21T08:01:55Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 14 review: 2 low-severity findings - ephemeral computed per menu item re-eval, and empty-string label not guarded in contact link text chain

---
- **time:** 2026-06-21T08:06:57Z
- **agent:** developer
- **status:** completed
- **message:** Step 14 review fixes: replaced per-item computed() dishNameById call with plain resolveDishName() (no per-item ComputedSignal); empty/whitespace contact-link label now falls back to kind-derived text so aria-label is never empty (WCAG 4.1.2); added test. app tests 125 passed; lint clean; format clean; build OK.

---
- **time:** 2026-06-21T08:11:08Z
- **agent:** qa
- **status:** completed
- **message:** Step 14 verified: build green (2 pre-existing style budget warnings only), lint clean, Prettier clean, all 3 new spec files (22 tests) + full suite app:125 + core:291 all green. All acceptance criteria confirmed.

---
- **time:** 2026-06-21T12:18:24Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 15: opt-in geolocation + distance toggle. New core GeolocationService (typed result, low-accuracy, denied/unavailable/timeout/unsupported -> i18n keys) + GeoOptInStore (persisted boolean, default OFF). New consumer GeoOptInComponent wired into query-controls; opt-in acquires coords -> SelectionStore.setGeo, off -> clearGeo (omits userGeo). search event now carries opt-in lat/lng FIELDS (no geo kind). core 299/299, app 134/134, lint+format clean, build green.

---
- **time:** 2026-06-21T12:22:43Z
- **agent:** developer-reviewer
- **status:** in-progress
- **message:** Reviewing Step 15 geo opt-in implementation

---
- **time:** 2026-06-21T12:23:46Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 15 review complete: 1 low-severity issue found (writeStored persists false string instead of removing key)

---
- **time:** 2026-06-21T12:26:50Z
- **agent:** developer
- **status:** completed
- **message:** Step 15 LOW fix: geo-optin writeStored removes key on opt-out instead of storing 'false'; updated spec to assert key removed. core 299 passed, app 134 passed, lint clean, prettier clean, build ok.

---
- **time:** 2026-06-21T12:30:23Z
- **agent:** qa
- **status:** completed
- **message:** Step 15 verified: build clean (2 pre-existing style-budget warnings, non-blocking), lint+format pass on both app and core projects, 299/299 tests green across 31 files. Confirmed: default OFF, geo API not called until explicit opt-in, clearGeo uses destructuring omit so userGeo absent from wire body, no geo EventKind in domain union, only boolean persisted (coords dropped on opt-out via removeItem), graceful failure across all four reasons with i18n keys, injectable mockable port, Material slide-toggle + a11y, no any, scope correct (service/store in core, toggle in consumer tree importing via barrel).

---
- **time:** 2026-06-21T12:43:34Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 16: added core AnalyticsEmitterService (batching, size/time/pagehide flush, best-effort swallow, impression dedupe per result set, unknown-kind guard) + spec; rewired results/map/details/query-controls/selection features off direct INGEST_ANALYTICS onto the single emitter path; updated 5 component specs to flush. build/lint/format green; core 307 + app 134 tests pass.

---
- **time:** 2026-06-21T12:48:22Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 16 review: 4 issues found — stale forward-looking JSDoc comments (low), session event not wired at app start (medium), pagehide flush not tested (low), resultSetToken includes userGeo causing false new-set impression on geo toggle (low)

---
- **time:** 2026-06-21T12:55:31Z
- **agent:** developer
- **status:** completed
- **message:** Step 16 reviewer fixes: session-boundary emission via provideAppInitializer (emitSessionStart, geo as FIELDS only when opted in, never a geo kind) + spec; refreshed 3 stale JSDoc comments; added pagehide flush test; resultSetToken now omits userGeo + no-reimpression test. core 308 pass, app 139 pass, lint/format/build green.

---
- **time:** 2026-06-21T13:00:39Z
- **agent:** qa
- **status:** completed
- **message:** Step 16 verified: build green (2 pre-existing SCSS budget warnings, Step 25 concern); 32+21=53 suites / 308+139=447 tests all green; lint+Prettier clean. Single path confirmed (AnalyticsEmitterService is sole INGEST_ANALYTICS caller in consumer production code; all feature containers inject emitter not token). Best-effort confirmed (buffer cleared before flush, catchError->EMPTY, dropped on failure). All 5 flush triggers tested (size, debounce, visibilitychange->hidden, pagehide, destroy). Session event wired via provideAppInitializer(emitSessionStart); geo fields on search/session, never a geo kind. resultSetToken omits userGeo. No PII, no any, no geo kind, no backend work pulled forward.

---
- **time:** 2026-06-24T17:20:12Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 17 i18n reconciled+completed: ngx-translate runtime catalogs (uk default+fallback, en placeholder) in core/i18n; LocaleService drives catalog+document.lang+persist+active-locale signal; replaced app-shell placeholder with real LanguageSwitchComponent; folded all deferred copy (errors.*, rating.*, consumer.geo.error.*, selection/browse/search/map/query/details labels) into uk catalog and rewired filters/category-browser/prefix-search/selection-summary/selection-builder/query-controls/map-modal/geo-optin/restaurant-details off raw literals to | translate; menu prices now follow active locale; LOCALE_ID+registerLocaleData(uk). Fixed broken filters-control (label/hint -> labelKey/hintKey). Added @angular/compiler to a test-only build config so ngx-translate JIT resolves; added providersFile global TranslateService for app specs. Tests: core 324 pass, app 143 pass; lint/format/build green.

---
- **time:** 2026-06-24T17:27:39Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 17 i18n review: 1 medium issue (misleading LOCALE_ID comment implies it tracks language switch; 2 low issues (as Signal<string> type cast; ad-slot spec asserts translated text without verifying source of translate via test-providers global mechanism)


---
- **time:** 2026-06-24T17:37:00Z
- **agent:** developer
- **status:** completed
- **message:** Step 17 revision (3 reviewer findings fixed). MEDIUM: rewrote the LOCALE_ID comment in app.config.ts to state it is PINNED to uk and does NOT follow the runtime language switch — all locale-aware formatting goes through the Intl-based core formatters that follow LocaleService.activeLocale(); adding an Angular locale-aware pipe later requires making this provider follow the active locale. LOW #1: added a single shared typed helper translateText(translate,key,params?) in core/src/i18n/translate-text.ts (exported via i18n barrel + public-api) that wraps TranslateService.translate and narrows to Signal<string> in one place, documenting the "catalog keys are leaf strings" invariant; migrated all 14 `as Signal<string>` casts off it (result-card, error-state, match-control, sort-control, restaurant-details, geo-optin, map-modal) and removed the now-unused Signal type imports. LOW #2: added a comment in ad-slot, sort-control, match-control specs noting the translate service is supplied globally via projects/app/src/test-providers.ts (providersFile), so assertions render uk text not raw keys. Outcomes: ng build green; lint green (app+core); prettier --check green; vitest 143 passed / 22 files. Step left [~] in-progress for qa.
---
- **time:** 2026-06-24T17:44:14Z
- **agent:** qa
- **status:** completed
- **message:** Step 17 verified: build green (3 pre-existing budget warnings only), 324 core + 143 app tests all pass, lint+Prettier clean. i18n catalog keys resolve to uk strings, deferred keys (errors/rating/geo) confirmed in catalog, language-switch persists+updates lang attr, Intl formatters locale-aware, no hardcoded strings, additive-locale proof present.

---
- **time:** 2026-06-24T17:52:19Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 18: operator portal shell + role-gated lazy boundary. Added portal-shell/portal-home/portal-nav, adminGuard on the portal subtree, uk portal i18n keys, nav-width token. Build/test/lint/format green (core 324, app 155); boundary probe confirms consumer->portal import is rejected.

---
- **time:** 2026-06-24T17:56:19Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 18 review: 2 issues found — (1) canActivateChild test only asserts length=1, not that adminGuard is the child guard; (2) no nav test exercises canActivateChild by navigating to a child path


---
- **time:** 2026-06-24T18:02:00Z
- **agent:** developer
- **status:** completed
- **message:** Step 18 review fixes (tests only, no feature change). Issue 1: portal.routes.spec now asserts route.canActivateChild toContain(adminGuard) (was length-only). Issue 2: added a nested describe that resets TestBed and provides the real portal parent route plus a throwaway eager child ('/menu'), then navigates the child deep-link under all three auth states — admin admitted, authed non-admin blocked (no login), anonymous blocked + login initiated — proving canActivateChild gates nested feature routes. app suite 158 passed (portal spec 8, up from 5); core unchanged. build/lint/format green.
---
- **time:** 2026-06-24T18:05:41Z
- **agent:** qa
- **status:** completed
- **message:** Step 18 verified: build green (3 pre-existing style-budget warnings, non-blocking), 24 app suites/158 tests + 35 core suites/324 tests all green, lint+Prettier clean. Portal spec: 8 route tests (lazy chunk, adminGuard on canActivate+canActivateChild, admit-admin, deny-non-admin, deny-anon, 3 child deep-link inheritance tests) + 7 shell tests (nav skeleton, identity, logout, login, denied state, skip link, no 'updated' copy) all pass. Build output confirms portal-shell-component and portal-home-component are separate lazy chunks. ESLint boundary config verified: consumer->portal and portal->consumer both disallowed. No actual cross-imports in production source. No 'any' in portal files. UK catalog has all portal.* keys. --dp-layout-portal-nav-width token present in _tokens.scss.

---
- **time:** 2026-06-25T05:30:26Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 19: built 4 lazy admin-gated portal features (menu mgmt + re-categorise, do-not-parse/do-not-update protection flags, address+regeocode, photo permission) wiring EDIT_MENU_ITEM/SET_MENU_ITEM_DO_NOT_PARSE/SET_RESTAURANT_DO_NOT_UPDATE/EDIT_ADDRESS/SET_PHOTO_PERMISSION; shared RestaurantPicker+SaveFeedback; portal.routes registers all 4 as loadChildren; uk portal.* keys added. app 188/188, core 324/324, lint+format clean, build green.

---
- **time:** 2026-06-25T05:38:56Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 19 review: 2 real issues found — (1) unused TranslateService injection in RestaurantPickerComponent; (2) fire-and-forget subscribe() without takeUntilDestroyed in ProtectionFlagsComponent. Endpoint wiring is correct (intentional doubled /admin path via proxy stripping). All acceptance criteria met.

---
- **time:** 2026-06-25T05:44:37Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 19: fixed reviewer LOW issues — removed dead TranslateService injection in restaurant-picker; added DestroyRef + takeUntilDestroyed to both protection-flag raw writes (menu/address/photo already use LoadingService.run which has teardown). format/lint clean, tests 324+188 pass, build exit 0.

---
- **time:** 2026-06-25T05:50:58Z
- **agent:** qa
- **status:** completed
- **message:** Step 19: build green (4 pre-existing warnings only), 30 app suites/188 tests + 35 core suites/324 tests all green, lint+Prettier clean, all 4 portal features verified.

---
- **time:** 2026-06-25T05:59:24Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 20: Verified subscription (grant Basic/Pro + revoke) and labeled ad-placement portal features as lazy admin-gated child routes; partner-status + labeled-paid-slot compliance copy; date-range validation. Added uk catalog keys. build OK; app 202 tests, core 324 tests pass; lint+format clean.

---
- **time:** 2026-06-25T06:07:01Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 20 reviewed: verified/ads components, data-access port, catalogs, tests, and compliance copy — all correct; no issues found

---
- **time:** 2026-06-25T06:15:09Z
- **agent:** qa
- **status:** completed
- **message:** Step 20: All acceptance criteria verified. build green (warnings are Step 25 budget issues); 202 app + 324 core tests pass; lint clean; Prettier clean. Verified: grant sends Basic=1/Pro=2 ordinal via PUT 204, revoke via DELETE 204, ad POST {targetingKey,startsAt,endsAt} returns 201 {id}. Date-range validator rejects startsAt>=endsAt; ISO conversion correct. Invariant #10 copy confirmed in uk.catalog.ts: partner-status framing + 'не «знак якості від нас»' for Verified; 'платний рекламний слот'+'«Реклама»'+'Органічний рейтинг не продається' for ads. No any; tokens only; no consumer imports; admin-gated lazy child routes.

---
- **time:** 2026-06-25T06:27:44Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 21: added IRatingRepository write port (Domain) + DapperRatingRepository adapter + RatingsSql write statements; new Ratings.Application project with SubmitRatingCommand/Handler + IRatingGivenPublisher port; MassTransitRatingGivenPublisher adapter + AddRatingsApplication DI extension in Infrastructure (not Application, to satisfy layer fitness test). Solution builds 0 warn/0 err. Unit tests 6/6 pass, Domain 29/29, Architecture 34/34. Write-side integration tests written but require Docker (unavailable here) — compile + wired, mirror existing aggregate suite.

---
- **time:** 2026-06-25T10:53:16Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 21 review: solution builds clean (0 warnings), 6/6 unit tests pass, 34/34 arch tests pass. One low-severity finding: WhereToEat.Ratings.Application.UnitTests GUID {9DFEC056} is missing from the NestedProjects section in src/WhereToEat.sln so the test project appears at the solution root in VS instead of under 03-Tests.


---
- **time:** 2026-06-25T11:05:00Z
- **agent:** developer
- **status:** completed
- **message:** Step 21 review fix: added NestedProjects entry {9DFEC056-4AF2-43A4-821B-4FDEE614CBE0} = {CD9DC6F0-73A3-47D5-89B0-2F83586ADBC5} in src/WhereToEat.sln so WhereToEat.Ratings.Application.UnitTests nests under 03-Tests with the other Ratings test projects. Re-verified: solution builds 0 warn/0 err; Ratings.Application unit tests 6/6 pass. Did not touch pre-existing Admin/Analytics nesting omissions (out of scope). Step stays [~] in-progress for qa.
---
- **time:** 2026-06-25T10:58:49Z
- **agent:** qa
- **status:** completed
- **message:** Step 21 verified: dotnet build clean (0 warnings/errors), 6/6 Ratings.Application unit tests pass (create path, revise path, score validation, RatingGiven published), 34/34 architecture tests pass (Application_DependsOnNoInfrastructureAndNoHost confirmed). Integration tests (RatingPersistenceTests): Docker daemon not accessible via named pipe in this process context — environment limitation, test compiles and logic verified by inspection. Worker AddRatingsPersistence unchanged, no HTTP endpoint added, no schema change.

---
- **time:** 2026-06-25T11:16:45Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 22: authenticated POST /restaurants/{id}/ratings on Public host (AddRatingsApplication wired, sub->opaque Guid, 204/400/401) + frontend rating-submit UI (data-access op, /ratings suffix allowlist, sign-in gate, smoothed-note, rating_given emission). Backend solution build 0/0; Ratings.Application unit 6/6; PublicApi integration test compiles+wired (Docker unavailable). Frontend build clean, core 327/327, app 209/209, lint+prettier clean.

---
- **time:** 2026-06-25T11:24:53Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 22 review: 3 findings — (1) MEDIUM: select() method comment says 'clears any prior success/error' but only resets succeeded, not the RequestState error signal; error persists until next submit; misleading comment + missing UX behavior + no test for this path. (2) LOW: integration test RatingEndpointTests.cs omits the 'RatingGiven published' assertion required by the step's testing requirements — the fixture has no IBusTestHarness override so the publish side-effect is unverifiable from the current test. (3) LOW: 'chooseScore' catalog key in uk.catalog.ts is defined but never referenced in the component or spec — dead key + missing affordance for authenticated users who hit submit with no star selected (button is disabled-silently, no visible explanation).

---
- **time:** 2026-06-25T11:34:01Z
- **agent:** developer
- **status:** completed
- **message:** Step 22 reviewer fixes: (1) select() now calls state.reset() to clear stale 400 error on star change + spec; (2) PublicApi integration RatingGiven-published test via dedicated AddMassTransitTestHarness swap (compile-only, Docker unavailable) + MassTransit/Contracts refs; (3) chooseScore wired as visible hint when no star selected + spec. Backend build 0 warn/0 err, Ratings unit 6/6 pass. Frontend core 327/327, app 211/211, lint clean, prettier clean, build OK.

---
- **time:** 2026-06-25T11:45:24Z
- **agent:** qa
- **status:** completed
- **message:** Step 22 verified: backend build clean (0w/0e), Ratings.Application unit tests 6/6 green, PublicApi integration tests compile+wired (Docker named-pipe env limitation, same as all integration suites); Angular core 327/327 + app 211/211 all pass, lint clean, build clean (budget warnings are Step 25). All acceptance criteria satisfied by inspection+test.

---
- **time:** 2026-06-25T15:50:19Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 23 reconciled+completed: Analytics App/Infra rollup-reader already done (verified vs schema); added AddAnalyticsReadModule DI, AdminApi AnalyticsDashboardEndpoints (admin-gated, window-validated 400), host registration+csproj ref; new Analytics+AdminApi integration tests + AddAnalyticsReadModule unit tests. Build 0/0 warnings-as-errors; Analytics.Application unit 28/28, Architecture 34/34; Docker-gated integration tests compile+wired (Docker unavailable).

---
- **time:** 2026-06-25T15:55:05Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 23 review: 2 issues found — (1) shape-only no-leak test covers only RestaurantTrafficSummary, not DemandBreakdown/PricePositioning/RatingsDistribution/ConversionFunnel; (2) StageCounts.Actions is non-nullable long but SQL SUM can return NULL — needs null guard or nullable property

---
- **time:** 2026-06-25T15:57:52Z
- **agent:** developer
- **status:** completed
- **message:** Step 23 reviewer fixes: parametrized invariant-#11 shape no-leak test over all 8 aggregate DTO types (incl. nested DemandCount/DishPricePosition); COALESCE-zeroed SelectRestaurantTraffic SUMs at SQL level and switched dead QuerySingleOrDefault guard to QuerySingleAsync. Build 0 warnings/0 errors; analytics unit 28/28, architecture 34/34 green. Integration tests Docker-gated, compiled+wired (Docker unavailable here).

---
- **time:** 2026-06-25T16:02:05Z
- **agent:** qa
- **status:** completed
- **message:** Step 23: dotnet build clean (0w/0e); 28/28 unit tests green (AddAnalyticsReadModuleTests 3 incl. read-only DI; AddAnalyticsModuleGuardTests 6; AnonymizerTests 9; IngestEventCommandHandlerTests 4; RotatingSaltProviderTests 4; others 2); 34/34 arch tests green; Docker daemon inaccessible (named pipe not available in Bash context) — Analytics.Integration and AdminApi.Integration are env-limited; invariant #11 shape verified by inspection for all 8 aggregate record types; SQL/DI/endpoint wiring confirmed by source review. Step marked done.

---
- **time:** 2026-06-25T16:20:24Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 24: analytics-dashboard portal feature + admin data-access ports (traffic/funnel/demand/price/ratings) wired to /admin/analytics. Core 335 pass, app 222 pass, build exit 0, lint clean, prettier clean. e2e dashboard journey deferred to Step 26 per plan.

---
- **time:** 2026-06-25T16:27:23Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 24: found 3 issues: (1) MatTableModule unused import in demand-breakdown.component.ts, (2) dead trackBySelection method in demand-breakdown.component.ts, (3) raw px/rem values (1px borders, 8rem/14rem/18rem minmax) not going through design-token variables in new metric panel components — token --dp-border-width-hairline exists but is not used

---
- **time:** 2026-06-25T16:33:45Z
- **agent:** developer
- **status:** completed
- **message:** Step 24: fixed 3 LOW reviewer cleanups — removed unused MatTableModule import + dead trackBySelection in demand-breakdown; replaced raw 1px borders with --dp-border-width-hairline across 5 panels; added --dp-column-min-width-sm/-md/-lg tokens and referenced them in metric grids. format/lint pass, 222 app tests + core green, build complete.

---
- **time:** 2026-06-25T16:41:49Z
- **agent:** qa
- **status:** completed
- **message:** Step 24: all acceptance criteria verified. Build green (budget warnings are pre-existing Steps 25 concern). 35 core tests (335 passing) + 35 app tests (222 passing). Lint clean, format clean. 5 admin analytics operations verified: correct /api/admin/admin/analytics/... paths via ApiConfig.adminUrl, aggregates-only DTOs match Step 23 IAnalyticsRollupReader records field-for-field. No . Dashboard gated under adminGuard lazy route; independent per-metric RequestState; privacy note present; uk i18n complete; design tokens (--dp-column-min-width-{sm/md/lg}) defined; chart bars aria-hidden with text equivalents; no consumer↔portal boundary violations.

---
- **time:** 2026-06-25T20:54:53Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 25: reconciled interrupted attempt (loading.service NG0203 fix making 23 RED core tests green + angular.json budget justification); RequestState teardown audit (all auto-tear-down via DestroyRef); tokenized 7 flex-basis + 4 hairline-border literals; result-card consolidated rating sr-only label. Build WARNING-FREE; core 336/336 + app 224/224 = 560 green; lint+prettier clean.

---
- **time:** 2026-06-25T21:01:11Z
- **agent:** developer-reviewer
- **status:** in-progress
- **message:** Reviewing Step 25 diff

---
- **time:** 2026-06-25T21:02:40Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 25 review complete: one finding - missing test coverage for multi-component DestroyRef isolation in LoadingService; all other checks passed

---
- **time:** 2026-06-25T21:06:44Z
- **agent:** developer
- **status:** completed
- **message:** Step 25: added multi-instance DestroyRef isolation test to loading.service.spec.ts (two HostComponent fixtures; destroy one, assert its emission is torn down; assert the other still-alive owner's emission lands). core test 337 passed (was 336), app 224 passed, lint clean, prettier clean, build warning-free.

---
- **time:** 2026-06-25T21:12:24Z
- **agent:** qa
- **status:** completed
- **message:** Step 25: Build warning-free (initial 650kB < 700kB warn, anyComponentStyle 6kB/8kB ceilings intact). 337 core + 224 app tests green. Lint clean. LoadingService.create() auto-teardown via resolveDestroyRef() confirmed with multi-instance isolation test. Tokens: --dp-field-basis-*, --dp-border-width-hairline present; no raw rem/border literals in portal styles. A11y: .dp-sr-only rating consolidation with aria-hidden visual fragments, skip links on both shells, reduced-motion media query, focus-trap/ESC/focus-return on map modal, accessible dl-based dashboard charts, labeled landmarks. Perf: all routes lazy (loadComponent/loadChildren), OnPush on every component, catalog caching, debounceTime(250) prefix search, CDK virtual scroll at >20 results, lazy/responsive generic photos. Step 26 (axe/e2e) correctly not pulled forward.

---
- **time:** 2026-06-25T21:37:13Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 26: wired Vitest coverage gates (core 90/85/95/90, app 80/75/70/80) in angular.json + test:coverage script; authored 4 Playwright e2e specs (consumer happy-path + 3 axe a11y scans runnable via route-mocks; rating + admin-portal journeys live-stack-gated). Ran: unit 561 green; core coverage 97.1/93.82/99.63/97.21 + app 87.37/82.6/76.13/86.14 (gate exit 0; verified non-zero on breach); e2e 4 passed / 2 skipped; lint+prettier+build green; backend build 0/0 + Ratings.App 6/6, Analytics.App 28/28, Arch 34/34. Infra-blocked: Docker integration suites + live-stack e2e (no Docker/Keycloak).

---
- **time:** 2026-06-25T21:42:53Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 26 review: coverage gate is genuine and thresholds are correctly calibrated; e2e specs are meaningful with stable testids and real invariant assertions; live-stack skip is correct Playwright API; one real issue found: no CI workflow file means the coverage gate is not enforced in any automated pipeline — acceptance criterion 'Unit coverage gate enforced in CI' is unmet. Also one low finding: @vitest/coverage-v8 listed as devDependency without a peerDependency constraint note.

---
- **time:** 2026-06-25T21:48:28Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 26: added .github/workflows/ci.yml (GitHub Actions) enforcing frontend gates on push/PR — Node 22 + npm ci in web/, then test:coverage (core 90/85 & app 80/75 gates), lint, build, e2e (playwright install --with-deps chromium). Re-ran all gates locally green: 561 unit tests pass; core 97.1%/93.82%, app 87.37%/82.6%; lint/build green; e2e 4 passed / 2 skipped (E2E_LIVE_STACK unset). YAML validated.

---
- **time:** 2026-06-25T21:56:28Z
- **agent:** qa
- **status:** completed
- **message:** Step 26 done: coverage gates pass (core 97.2/94.14/99.63/97.21 >> 90/85/95/90; app 87.37/82.6/76.13/86.14 >> 80/75/70/80); 561 unit tests green (337 core + 224 app); e2e 4 passed / 2 skipped (live-stack rating+portal skip cleanly); lint all pass; build warning-free; backend unit+arch 319 tests green; Docker-gated Testcontainers suites infra-blocked (consistent with prior steps)

