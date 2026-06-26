# Implementation — frontend-app

> Executable decomposition of the **approved** `design.md`. The bulk is an Angular workspace
> (consumer search app + operator portal) wired to the existing two-host .NET backend; two scoped
> backend additions (rating-submit, analytics dashboard read) are delivered fully. Sequenced so the
> repo stays buildable/testable after each step.
>
> **Conventions used throughout (do not repeat per step):**
> - **Workspace root for the SPA:** `web/` at repo root (a new Angular CLI workspace; the .NET
>   `src/` and `tests/` trees are untouched except by the two backend-addition steps).
> - **Angular:** latest stable Angular, **standalone components only** (no NgModules), **signals**
>   for state, `ChangeDetectionStrategy.OnPush` on every component, **lazy-loaded** routes per
>   feature, typed `HttpClient`. State stores are a **lightweight hand-rolled signal store** base
>   class (`signal`/`computed`/explicit mutator methods) — chosen over NgRx SignalStore to keep the
>   store shape a transparent 1:1 blueprint for the RN rewrite and avoid a heavy dependency; it is
>   applied consistently to every store.
> - **UI:** Angular Material 3 themed from a **design-token** layer (SCSS custom properties); no
>   component reads raw hex/spacing values.
> - **i18n:** `@ngx-translate/core` runtime catalogs (chosen over Angular compile-time `$localize`
>   so the wired language switch can swap locale at runtime with no rebuild — justified in Step 17);
>   default and only populated locale `uk`; every string catalog-keyed.
> - **API access:** SPA always uses **relative `/api/...`**; the data layer composes the public vs
>   admin **route prefix** from environment config behind one gateway origin. Backend error envelope
>   is `{ error, message }`; admin writes return **204 No Content** (404/400 on failure); recommend
>   and analytics-ingest return the documented success codes.
> - **Auth:** `angular-oauth2-oidc` (OIDC Authorization Code + PKCE) against Keycloak; bearer
>   attached only on calls that need it (rating-submit, all admin/portal calls, analytics-read).
> - **Cross-cutting invariants every UI step must honor:** deterministic selection search, no NLP
>   (#1); two-level Category→Dish (#2); explicit sort dominates, coverage is a tie-breaker only (#5);
>   our smoothed/cumulative rating with a low-review note (#6); live map, nothing cached, no Google
>   rating/coords stored (#7, §5.7); always-free contact links (§5.8, #10); generic dish photos by
>   default (#8); labeled+separated ad slots (#10); minimal discreet `≈`/`ⓘ` disclaimer, **no
>   "updated X days ago"**, no per-item disclaimers (#12, §10); opt-in approximate geo, aggregates-
>   only analytics (#11).
> - **Definition of "buildable":** after each step `web/` builds (`ng build`) and its unit suite
>   passes; backend-addition steps additionally keep `dotnet build`/`dotnet test` green.

## Implementation Steps

### Step 1: Scaffold the Angular workspace, tooling, lint/test harness, and CI build
- **status:** [x] done
- **description:** Create the `web/` Angular CLI workspace that hosts both surfaces: one application
  project `app` plus a `core` library for shared business logic, so the consumer/portal boundary and
  the shared layer are clean. Enforce the module boundary with **ESLint import-boundary rules** (an
  import-restriction / boundaries rule — e.g. `@nx/enforce-module-boundaries` or
  `eslint-plugin-boundaries`, or `no-restricted-imports` path patterns) that forbid the consumer
  feature tree from importing portal internals and vice-versa, and forbid reaching into another
  area's internals except through its public-api barrel — ESLint is the actual enforcement mechanism
  in an Angular workspace (Angular has no runtime "project reference" boundary for lazy routes).
  Configure strict TypeScript, ESLint (Angular ESLint + the boundary plugin), Prettier,
  Karma/Jasmine (or Vitest via the Angular builder) for unit tests, and a Playwright project for
  e2e (empty for now). Add npm scripts (`build`, `test`, `lint`, `e2e`). Enable `strict` and
  `strictTemplates`. No features yet — this is the empty, building shell.
- **files to modify:** new `web/` workspace: `web/angular.json`, `web/package.json`,
  `web/tsconfig.json`, `web/tsconfig.app.json`, `web/.eslintrc`/`eslint.config.js` (Angular ESLint
  + the import-boundary rule config), `web/projects/app/...` (bootstrap app shell, `main.ts`,
  `app.config.ts`, root `app.component.ts`), `web/projects/core/...` (empty public-api barrel),
  `web/playwright.config.ts`, `web/karma.conf.js`/test config.
- **acceptance criteria:** `ng build` and `ng test` succeed on a clean checkout; lint passes; the
  app boots to an empty shell; `strict` + `strictTemplates` are on; `core` is a separate library the
  `app` consumes through its public-api barrel; the **ESLint import-boundary rules** are configured
  and `lint` fails on a deliberate cross-boundary import (consumer ↔ portal, or reaching past a
  public-api barrel) — so the boundary is enforced by ESLint, not by Angular project references. No
  backend or feature code added.
- **testing requirements:** One trivial smoke unit test (root component renders) passes under the
  configured runner; CI `build`+`test`+`lint` scripts exit 0.

### Step 2: Design-token system + Angular Material 3 theme (mobile-first, WCAG, responsive shell)
- **status:** [x] done
- **description:** Install Angular Material and define the **design-token layer** first (SCSS custom
  properties for color, spacing, typography scale, radius, elevation, and a harmonious accessible
  palette structure), then derive the Material 3 theme from those tokens so components consume tokens
  not raw values (DRY, brand-tunable later without architectural change). Build the responsive
  app-shell scaffolding (mobile-first layout primitives, a top bar with a placeholder language
  switch slot, a content outlet, a global footer hosting the single discreet `ⓘ` data-accuracy
  notice — invariant #12/§10). Set up global typography, focus-visible styles, and high-contrast
  tokens that meet WCAG AA contrast.
- **files to modify:** `web/projects/app/src/styles/_tokens.scss`,
  `web/projects/app/src/styles/_theme.scss`, `web/projects/app/src/styles.scss`,
  `web/projects/app/src/app/shell/app-shell.component.ts` (+ template/styles),
  `web/projects/app/src/app/shell/footer.component.ts`, `web/angular.json` (style assets).
- **acceptance criteria:** Material renders themed from tokens; no component template references a
  raw hex/spacing literal (lint rule or review check); the shell is responsive at phone/tablet/
  desktop breakpoints; the footer shows exactly one discreet `ⓘ`/`≈` data-accuracy notice and there
  is **no** "updated X days ago" anywhere; AA contrast holds for text/background token pairs.
- **testing requirements:** Component test asserting the shell renders the footer notice once and a
  language-switch slot exists; a token-contrast unit check (computed contrast ratio ≥ 4.5:1 for
  body text token pairs).

### Step 3: Environment config + single-gateway dev proxy + logical-host route-prefix model
- **status:** [x] done
- **description:** Define environment configuration (`environment.ts` / `environment.prod.ts`)
  carrying: the API gateway origin (empty/same-origin in prod, dev-proxy in dev), the **public**
  and **admin** route prefixes (e.g. `/api/public`, `/api/admin`), Keycloak authority/clientId/
  redirectUri/scopes, the map-provider selection + Google Maps Embed credential slot, and feature
  flags. Add the Angular dev-server **proxy** config so relative `/api/...` reaches both backend
  hosts behind one origin in development (proxy `/api/public` → Public API port, `/api/admin` →
  Admin API port). Introduce a typed `ApiConfig`/`AppConfig` injectable that exposes the resolved
  prefixes so no host is hardcoded in feature code.
- **files to modify:** `web/projects/app/src/environments/environment.ts`,
  `web/projects/app/src/environments/environment.prod.ts`, `web/proxy.conf.json`,
  `web/angular.json` (serve proxyConfig), `web/projects/core/src/config/app-config.token.ts`,
  `web/projects/core/src/config/api-config.ts`.
- **acceptance criteria:** `ng serve` proxies `/api/public/*` and `/api/admin/*` to the two backend
  ports; no cross-origin host string appears in any feature file (only in environment/proxy config);
  `AppConfig` resolves both prefixes and the Keycloak/map config from environment; switching origin
  prefix requires only an environment change.
- **testing requirements:** Unit test that `ApiConfig` composes a public URL and an admin URL from
  config (prefix + path) and never emits an absolute cross-origin host; manual: a dev request to a
  public read endpoint succeeds through the proxy.

### Step 4: Domain models, enums, and contract types mirroring the backend
- **status:** [x] done
- **description:** In `core`, define the framework-light **domain model** TypeScript types that
  mirror the backend contracts exactly so the wire shape has one source of truth: `CategoryDto`,
  `DishDto`, `ContactLinkDto`, `MenuItemDto`, `RestaurantDetailsDto`, `MapPayloadDto`,
  `RecommendationRequest`/`SelectedItem`/`FilterSelection`/`UserGeo`, `RecommendationResultDto`/
  `RecommendedRestaurantDto`, the analytics raw-event shape, and the `/me` identity shape. Define
  the **contract key value** enums/unions: `MatchMode = 'or' | 'and'`,
  `SortMode = 'price' | 'distance' | 'rating' | 'price-quality' | 'best'`,
  `FilterKey = 'price' | 'rating'`, `EventKind` union mirroring the backend
  `Analytics.Domain.EventKind` enum **exactly — its eight members and no others** (the enum has **no
  `geo` member**; geolocation rides on other events as optional `latitude`/`longitude` fields, never
  as its own kind), including `rating_given` (the backend enum defines `RatingGiven = 6`) so the event
  type exists before Step 22 wires its emission:
  `'impression' | 'view' | 'card_open' | 'action' | 'search' | 'filter' | 'rating_given' | 'session'`.
  **Wire-casing (load-bearing):** the ingest endpoint parses the wire `Kind` with
  `Enum.TryParse<EventKind>(kind, ignoreCase: true)` + `Enum.IsDefined`, which matches the **C# member
  names** (`Impression`, `CardOpen`, `RatingGiven`, …) case-insensitively but does **not** strip
  underscores — so a literal `card_open`/`rating_given` would 400. The union members are the canonical
  **snake_case** in-app values; the data layer (Step 5) is responsible for emitting the `Kind` string in
  the spelling the parser accepts (the enum-member form, e.g. `CardOpen`/`RatingGiven`, or any casing of
  it) so ingest never rejects a known kind. Define the typed **error model** (`{ error, message }`
  envelope → app error type).
- **files to modify:** `web/projects/core/src/domain/*.ts` (one file per cohesive group),
  `web/projects/core/src/domain/contract-keys.ts`, `web/projects/core/src/domain/api-error.ts`,
  `web/projects/core/src/public-api.ts` (barrel exports).
- **acceptance criteria:** Every backend DTO field read in this plan has a matching typed field
  (names/optionality match the backend records, e.g. `RecommendedRestaurantDto` carries
  `basketPriceAmount?`, `basketPriceCurrency?`, `smoothedRating?`, `ratingCount`, `distanceKm?`,
  `coverage`); the match/sort/filter unions use exactly the contract key values; the `EventKind`
  union has **exactly the backend enum's eight members and no extras** (`impression`, `view`,
  `card_open`, `action`, `search`, `filter`, `rating_given`, `session`) — it **includes `rating_given`**
  (mirroring backend `RatingGiven = 6`) and **contains no `geo` member** (so the ingest endpoint, which
  rejects an unknown kind with a 400 that fails the whole batch, can never 400 on a kind this union can
  produce); the union maps 1:1 to `Analytics.Domain.EventKind`; no `any` in domain types; selecting an
  item type enforces "exactly one of categoryId/dishId" at the type level (discriminated union).
- **testing requirements:** Type-level unit tests / compile-time assertions that the `SelectedItem`
  union forbids both-ids and neither-ids; a unit test that the `SortMode`/`MatchMode`/`FilterKey`
  unions equal the documented contract strings.

### Step 5: Typed data-access layer — interfaces + HttpClient implementations for every endpoint
- **status:** [x] done
- **description:** Define **one operation interface per endpoint** (dependency inversion: features
  depend on interfaces) and an `HttpClient` implementation for each, choosing the correct logical-
  host prefix from `ApiConfig`. Cover **every** endpoint with exactly one operation each:
  **Public** — `getCategories()` (`GET /categories`), `getDishesByCategory(id)`
  (`GET /categories/{id}/dishes`), `getRestaurantDetails(id)` (`GET /restaurants/{id}`),
  `searchCategories(prefix, limit)` (`GET /search/categories?prefix=&limit=`),
  `searchDishes(prefix, limit)` (`GET /search/dishes?prefix=&limit=`), `recommend(body)`
  (`POST /recommend`), `getMap(restaurantId)` (`GET /map/{restaurantId}`),
  `ingestAnalytics(events)` (`POST /analytics/events`), `getMe()` (`GET /me`, authed),
  `getHealth()` (`GET /health`). **Admin** — `editMenuItem(id, body)`
  (`PUT /admin/menu-items/{id}`), `editAddress(id, body)` (`PUT /admin/restaurants/{id}/address`),
  `setMenuItemDoNotParse(id, value)` (`PUT /admin/menu-items/{id}/do-not-parse`),
  `setRestaurantDoNotUpdate(id, value)` (`PUT /admin/restaurants/{id}/do-not-update`),
  `setPhotoPermission(id, value)` (`PUT /admin/photos/{id}/permission`),
  `grantVerified(id, tier)` (`PUT /admin/venues/{id}/verified`), `revokeVerified(id)`
  (`DELETE /admin/venues/{id}/verified`), `createAdPlacement(id, body)`
  (`POST /admin/venues/{id}/ad-placements`). The recommend request is serialized to the exact wire
  body (`items[{categoryId|dishId}]`, `match`, `sort`, `filters[{key,value}]`,
  `userGeo{latitude,longitude}`). Admin write responses (204) and the `{ error, message }` failure
  envelope are mapped to typed results.
- **files to modify:** `web/projects/core/src/data-access/public/*.ts` (interface + impl per
  endpoint or grouped service files), `web/projects/core/src/data-access/admin/*.ts`,
  `web/projects/core/src/data-access/tokens.ts` (InjectionTokens for each interface),
  `web/projects/core/src/public-api.ts`.
- **acceptance criteria:** Each of the 10 public + 8 admin endpoints has exactly one operation
  behind an interface with an `HttpClient` implementation; public operations resolve the public
  prefix and admin operations the admin prefix; the recommend body matches the backend wire shape
  field-for-field; admin operations treat 204 as success and 404/400 `{error,message}` as typed
  failures; no operation hardcodes an origin.
- **testing requirements:** Unit tests with `HttpTestingController` per operation: asserts method,
  URL (correct prefix + path + query), request body shape, bearer presence/absence, and the
  204/200/400/404 response mapping. (Recommend serialization is asserted here against the wire shape.)

### Step 6: Auth foundation — Keycloak OIDC (Auth Code + PKCE), bearer interceptor, role gating
- **status:** [x] done
- **description:** Integrate `angular-oauth2-oidc` for OIDC Authorization Code + PKCE against
  Keycloak (authority/client/redirect/scopes from environment). Provide an `AuthService` exposing
  signals: `isAuthenticated`, `subject` (the IdP `sub`), and `roles` parsed from the token's role
  claim (handle both `realm_access.roles` and a flattened `role`/`roles` claim, matching the
  backend's Keycloak-style mapper). Add a **bearer interceptor** that attaches the token **only** to
  calls that need it (admin/portal calls, the rating-submit call once it exists, and analytics-read)
  and never to anonymous consumer reads. Add a `roleGuard`/`adminGuard` for portal routes. Wire the
  consumer-anonymous default (reads work with no token). Use the `/me` operation to verify the auth
  seam end-to-end.
- **files to modify:** `web/projects/core/src/auth/auth.service.ts`,
  `web/projects/core/src/auth/bearer.interceptor.ts`,
  `web/projects/core/src/auth/admin.guard.ts`,
  `web/projects/app/src/app/app.config.ts` (provide OAuth + interceptor), environment auth config.
- **acceptance criteria:** Login runs Authorization Code + PKCE against Keycloak and stores the
  token; `roles` signal reflects `admin`/`user` from the token regardless of realm-role vs flat
  claim placement; the bearer is attached to admin/rating/analytics-read calls and **absent** from
  anonymous public reads; `adminGuard` blocks non-admin tokens from portal routes; `getMe()` returns
  the subject/roles for an authenticated user and the call is 401 when unauthenticated.
- **testing requirements:** Unit tests: interceptor attaches bearer for an admin URL and omits it
  for an anonymous public URL; role parser maps both claim shapes to the `admin` role; `adminGuard`
  denies a user-only token and allows an admin token (mocked `AuthService`).

### Step 7: Error normalization + loading/UX cross-cutting + analytics session id
- **status:** [x] done
- **description:** Add a functional **error-normalization interceptor** mapping backend
  `{ error, message }` and HTTP 400/401/403/404/5xx to the typed app error model and to localized
  user-facing messages (keys resolved later by i18n). Add a small `LoadingService`/request-state
  primitive and a reusable error/loading presentational pattern (spinner, retry, empty-state) the
  features reuse so loading/error handling is consistent and DRY. Add a **session-id provider**
  (a per-session opaque id, persisted in session storage) that the analytics event builders will use
  as the client correlation id (the backend anonymizes server-side — invariant #11).
- **files to modify:** `web/projects/core/src/http/error.interceptor.ts`,
  `web/projects/core/src/ui-state/loading.service.ts`,
  `web/projects/app/src/app/shared/ui/error-state.component.ts`,
  `web/projects/app/src/app/shared/ui/loading.component.ts`,
  `web/projects/core/src/analytics/session-id.provider.ts`.
- **acceptance criteria:** All HTTP errors surface as the typed model (never a raw `HttpErrorResponse`
  in feature code); a shared loading/error/empty pattern exists and is used by at least the first
  consumer feature; the session id is stable within a session and changes across sessions; the
  session id is opaque (no PII).
- **testing requirements:** Unit tests: interceptor maps a 400 `{error,message}` and a 404 to the
  typed model with the right code; session-id provider returns a stable id within a session.

### Step 8: Portable business-logic services (request builder, validation, formatters, analytics builders, Haversine display)
- **status:** [x] done
- **description:** Implement the **isolated, well-named business-logic services / pure functions** in
  `core` — the layer whose cleanliness is the RN blueprint and the layer with the highest unit
  coverage:
  - **Recommendation request builder** — turns the selection list + match + sort + filters + optional
    `userGeo` into the exact `recommend` wire body; enforces "exactly one of categoryId/dishId" per
    item and a non-empty selection (mirrors the backend's pre-pipeline validation so the UI fails
    fast before a round-trip).
  - **Validation service** — selection validity, `userGeo` lat∈[-90,90]/lng∈[-180,180] range, match/
    sort/filter key validity (against the contract unions).
  - **Formatters** — locale-aware currency formatted **by the returned `PriceCurrency`** (not
    hardcoded ₴), distance in **km**, and **smoothed-rating presentation** including the discreet
    low-review-count note (invariant #6). **No per-item / per-price `≈` approximate marker** is
    produced here — the price-approximation disclaimer lives solely in the single discreet footer/`ⓘ`
    notice from Step 2 (invariant #12 / §10 forbids per-item disclaimers).
  - **Analytics event builders** — produce the exact `RawEventBody` payloads per `EventKind`
    (impression/view/card_open/action/search/filter/rating_given/session — the backend's eight kinds,
    **no `geo` kind**), stamping the session id; never include PII. The opt-in approximate location is
    **not** its own kind — builders attach optional `latitude`/`longitude` **fields** to the relevant
    event(s) (e.g. the `search`/`session` builder), which the server-side anonymizer coarsens to a
    geohash and drops (invariant #11).
  - **Haversine display helper** — distance for display only (the backend ranks; this is presentation).
- **files to modify:** `web/projects/core/src/recommend/recommend-request.builder.ts`,
  `web/projects/core/src/validation/recommend-validation.service.ts`,
  `web/projects/core/src/format/currency.formatter.ts`,
  `web/projects/core/src/format/distance.formatter.ts`,
  `web/projects/core/src/format/rating.presenter.ts`,
  `web/projects/core/src/analytics/event-builders.ts`,
  `web/projects/core/src/geo/haversine.ts`, barrels.
- **acceptance criteria:** The request builder output is byte-equivalent to the backend wire body for
  representative selections (category-only, dish-only, mixed, OR vs AND, each sort, with/without
  geo); validation rejects both-ids/neither-ids, out-of-range geo, and unknown keys with typed
  errors; currency formats by the supplied `PriceCurrency`; rating presenter emits the low-review
  note below a threshold and never shows a Google rating; analytics builders emit exactly the
  `RawEventBody` fields and carry no PII; all are framework-light pure/injectable services with no
  view coupling.
- **testing requirements:** **High-coverage unit tests** for every service (this is the RN-risk-
  reduction coverage the design calls for): request-builder table tests across modes; validation
  boundary tests; formatter locale/currency/distance tests; rating-presenter threshold tests;
  event-builder shape tests; Haversine numeric tests against known coordinate pairs.

### Step 9: Signal-store base + catalog/selection/results stores
- **status:** [x] done
- **description:** Implement the **hand-rolled signal-store base** (readable signals + `computed` +
  explicit, testable mutator methods, with a consistent loading/error sub-state) and the three
  cohesive stores: a **catalog store** caching the rarely-changing taxonomy (categories + dishes-by-
  category) fed by the data-access layer; a **selection store** holding the user's selected items +
  match + sort + filters + geo opt-in; and a **results store** holding the last recommend result and
  its request echo. Stores expose readable signals to smart components and never touch the DOM. Keep
  each store small and separated so its shape maps 1:1 onto an RN container.
- **files to modify:** `web/projects/core/src/state/signal-store.base.ts`,
  `web/projects/core/src/state/catalog.store.ts`,
  `web/projects/core/src/state/selection.store.ts`,
  `web/projects/core/src/state/results.store.ts`, barrels.
- **acceptance criteria:** Stores are signal-based with explicit mutators (no public setters on raw
  signals); the catalog store caches the taxonomy and does not re-fetch on repeat reads; the
  selection store enforces the selection rules via the validation service; the results store holds
  the ranked list in backend order (no client re-sort — invariant #5); stores are framework-light
  enough to mirror in RN (no component/DOM references).
- **testing requirements:** Unit tests per store: catalog caches and exposes categories/dishes;
  selection store add/remove/toggle-match/set-sort/set-filter transitions; results store stores and
  exposes the ordered result and clears on new request; loading/error sub-state transitions.

### Step 10: Consumer feature — selection builder (browse taxonomy + deterministic prefix search)
- **status:** [x] done
- **description:** Build the **deterministic selection flow** (invariant #1 & #2): a lazy-loaded
  consumer feature where the user browses categories → dishes-within-category, or uses the
  **anchored prefix-search typeahead** (debounced, calling `searchCategories`/`searchDishes`) to add
  a category or a dish to the selection list. Input only filters lists by prefix — **no free-text
  query reaches the engine**. Smart container injects the catalog/selection stores; dumb
  presentational components render lists, the selected-items chips, and the typeahead. Emit `search`
  analytics events on selection build.
- **files to modify:** `web/projects/app/src/app/consumer/selection/*` (container + presentational
  components + route), `web/projects/app/src/app/consumer/consumer.routes.ts`,
  root route registration in `app.config.ts`/`app.routes.ts`.
- **acceptance criteria:** Users can build a selection by browsing the two levels and by prefix
  search; the typeahead is anchored-prefix and debounced and never sends free text to `recommend`;
  each selected item is exactly one category or one dish; the feature is lazy-loaded; a `search`
  analytics event is emitted on build; keyboard navigation + screen-reader labels work on the
  list/typeahead (WCAG).
- **testing requirements:** Component tests: browsing populates dishes for a chosen category;
  typeahead debounces and adds a result to the selection; selection chips reflect adds/removes. Unit:
  the prefix-search query is anchored and capped by `limit`.

### Step 11: Consumer feature — match/sort/filters controls (explicit-sort-dominates UX) + recommend submit
- **status:** [x] done
- **description:** Build the **match (Or/And)**, **sort** (`price`/`distance`/`rating`/
  `price-quality`/`best`), and **composable filters** (price max, rating min) controls, plus the
  submit that calls `recommend` via the request builder and populates the results store. The filter
  UI is structured so new composable filters (open-now/vegan/…) can be added without redesign
  (invariant #4). The controls and copy make clear that **explicit sort dominates** and coverage is
  secondary (invariant #5). And-mode is labeled as "combo (all selected present)".
- **files to modify:** `web/projects/app/src/app/consumer/query-controls/*` (match/sort/filter
  presentational components + container), wiring into the selection container.
- **acceptance criteria:** Match toggles Or/And; sort offers exactly the five contract values;
  filters compose (price + rating) and the filter list is data-driven so a new filter key is additive;
  submitting builds the exact recommend body and stores the result; the UI copy/labels communicate
  explicit-sort-dominance and combo semantics; a `filter` analytics event fires on filter apply.
- **testing requirements:** Component tests: changing sort/match/filters updates the selection store
  and the next recommend body; a new filter key added to the filter config renders without code
  change. Unit: submit produces the correct wire body for each sort and match.

### Step 12: Consumer feature — results list (basket price, smoothed rating, distance, coverage, ad-slot contract)
- **status:** [x] done
- **description:** Render the **ranked results list** from the results store in **backend order**
  (no client re-sort). Each card shows: basket price **with no per-item price disclaimer or `≈`
  marker** (invariant #12 / §10 forbids "не під кожною стравою" — the single discreet footer/`ⓘ`
  data-accuracy notice from Step 2 is the sole carrier of the price-approximation disclaimer), our
  **smoothed rating** + count with the discreet low-review note (invariant #6), **distance in km**
  when geo was provided, and **coverage ("N of M") as clearly secondary** information (invariant #5).
  Use **generic dish
  photos as optimized, lazy-loaded, responsive assets** by default (invariant #8), swapping to real
  photos only where the backend signals permission. Structure the list to render a **clearly labeled,
  visually-separated ad slot** when present (presentation contract — the backend recommend response
  has no ad field yet; render labeled slots only when the contract is added; never blend ads into
  organic order). Emit `impression` events for shown cards and `card_open` on open. Virtualize/lazy-
  render long lists for performance.
- **files to modify:** `web/projects/app/src/app/consumer/results/*` (list container + result-card
  presentational + generic-photo component + ad-slot placeholder component).
- **acceptance criteria:** Cards render in the exact backend order; the basket price renders **with
  NO per-item price disclaimer and NO `≈` marker on the card** — the price-approximation disclaimer
  appears **only** in the single discreet footer/`ⓘ` notice established in Step 2; rating shows the
  smoothed value + count and a low-review note under threshold and never a Google rating; distance
  shows km only when geo present; coverage is visually secondary; generic photos are the default and
  lazy/responsive; an ad slot, if data is present, renders labeled and separated (and renders nothing
  when absent); `impression`/`card_open` events emit; long lists virtualize.
- **testing requirements:** Component tests: ordering preserved; the result card renders **no `≈`
  marker and no per-item price disclaimer** (asserted); low-review-note and coverage-as-secondary
  render rules; generic photo is default and real photo used only when permitted; ad-slot component
  renders a labeled separated slot for mock ad data and nothing otherwise; analytics events emitted
  on render/open.

### Step 13: Consumer feature — inline "View on map" via swappable map-provider interface
- **status:** [x] done
- **description:** Add the **"Глянути на карті / View on map"** affordance on each card that fetches
  the `/map/{restaurantId}` payload and opens a **live map inline** (modal/expansion) — no navigation
  into details required (§5.7). Put the map source behind a **map-provider interface** (swappable,
  RN-portable): the default provider is the **Google Maps Embed** with the credential from
  environment; **with no credential it degrades to a PlaceId / Maps deep-link** affordance rather
  than failing. Nothing is cached; no Google rating or Google coordinate is stored — the Google
  rating is visible only inside the live Embed. A restaurant with no stored coordinates
  (`HasMapData=false`) shows the well-defined "no map data" state. Manage focus for the modal (WCAG).
  Emit an `action` analytics event on open.
- **files to modify:** `web/projects/core/src/map/map-provider.ts` (interface + token),
  `web/projects/core/src/map/google-embed.provider.ts`,
  `web/projects/core/src/map/deep-link.provider.ts`,
  `web/projects/app/src/app/consumer/map/map-modal.component.ts`, card wiring.
- **acceptance criteria:** Clicking the affordance fetches the map payload and opens the live map
  inline without leaving the list; with an Embed credential the Embed renders, without one it
  degrades to a PlaceId/deep-link affordance; nothing from the map is cached and no Google rating/
  coord is persisted; `HasMapData=false` shows the no-map state and a missing restaurant yields the
  not-found state; modal traps/restores focus; an `action` event emits.
- **testing requirements:** Component test: open fetches payload and renders the active provider;
  degraded path renders the deep-link when credential absent; no-map-data state renders for a
  `HasMapData=false` payload. Unit: provider selection chooses Embed when credential present, deep-
  link otherwise.

### Step 14: Consumer feature — restaurant details (always-free contact links, menu, generic photos)
- **status:** [x] done
- **description:** Build the lazy-loaded **restaurant details** view from `GET /restaurants/{id}`.
  It **always** shows the contact links (site/social/phone) for free, even for non-Verified venues
  (§5.8, invariant #10 — contact links are never monetized). It renders the menu items (price by
  `PriceCurrency`, optional weight) with **generic dish photos by default**, real photos only where
  permitted. No "updated X days ago", no per-item price/photo disclaimers (the single footer notice
  covers it). Emit `card_open`/`view` and `action` (on a contact-link click) analytics events.
- **files to modify:** `web/projects/app/src/app/consumer/details/*` (container + presentational menu
  list + contact-links component + route), route registration.
- **acceptance criteria:** Details render name/address/menu; contact links are always present (even
  for a non-Verified venue) and labeled, with no paywall; menu prices format by `PriceCurrency`;
  generic photos are the default with real photos only when permitted; no per-item disclaimer and no
  "updated X days ago"; `action` emits on a contact-link click.
- **testing requirements:** Component tests: contact links render for a venue regardless of Verified;
  menu formats currency/weight; generic-vs-real photo rule; analytics events on view/contact click.

### Step 15: Consumer feature — opt-in geolocation + distance ranking toggle
- **status:** [x] done
- **description:** Implement **opt-in approximate geolocation** (invariant #11): distance ranking is
  on **only when** the user has opted in; the user can decline or disable to range further for price/
  quality. On opt-in, acquire approximate coordinates (browser geolocation, coarse) and feed
  `userGeo` into the selection store so the request builder includes it; on decline/disable, omit
  `userGeo` entirely (and distance fields are not shown). Precise coordinates are never required.
  When opted in, the approximate coordinates additionally ride as optional `latitude`/`longitude`
  **fields on the relevant analytics events** (e.g. the next `search`/`session` event) — there is
  **no `geo` analytics event kind** (the backend `EventKind` enum has none; sending one would 400 the
  batch); the server-side anonymizer coarsens these fields to a geohash and drops them (invariant #11).
  Persist the opt-in choice.
- **files to modify:** `web/projects/app/src/app/consumer/geo/geo-optin.component.ts`,
  `web/projects/core/src/geo/geolocation.service.ts`, selection-store wiring.
- **acceptance criteria:** Distance ranking is off by default until opt-in; opt-in adds `userGeo` to
  the recommend body and enables km display; declining/disabling omits `userGeo` and hides distance;
  coordinates are approximate (no precise requirement); when opted in the approximate `latitude`/
  `longitude` ride as **fields on the relevant analytics event** (e.g. `search`/`session`) and **no
  `geo` event kind is emitted** (none exists in the backend enum); the choice persists across sessions.
- **testing requirements:** Component/unit tests: opt-in toggles `userGeo` presence in the next
  recommend body; decline omits it; opt-in stamps `latitude`/`longitude` onto the relevant analytics
  event payload (and **no event with a `geo` kind is produced**); geolocation service returns coarse
  coordinates (mocked) and handles denial gracefully.

### Step 16: Consumer analytics emission wiring (event batch via ingest endpoint)
- **status:** [x] done
- **description:** Wire the analytics **event builders** (Step 8) and the **ingest operation**
  (Step 5) into a small batching emitter that flushes the raw event batch to `POST /analytics/events`.
  The emitter accepts **any** `EventKind` (the full Step 4 union, including `rating_given`) so it is
  not tied to a fixed list; this step wires the consumer-originated kinds (impression / view /
  card_open / action / search / filter / session — the backend's eight kinds, **no `geo` kind**) from
  their source containers, and leaves `rating_given` as a ready slot the emitter already accepts —
  **Step 22 owns wiring the `rating_given` emission** when the rating-submit UI lands (no code change
  needed here for it). The **opt-in approximate-geolocation signal is emitted as optional
  `latitude`/`longitude` fields riding on the relevant events** (e.g. the `search`/`session` event
  emitted on geo opt-in), **never as a `geo` event kind** — the backend `EventKind` enum has no `geo`
  member and its ingest endpoint 400s the whole batch on an unknown kind. The frontend uses the
  session id; the backend anonymizes the lat/lng and ids server-side (invariant #11). Ensure events
  fire from the right places (results render, card open, contact action, map action, search/filter
  apply, geo opt-in → lat/lng on the `search`/`session` event, session start). These are the very
  signals the admin dashboard later rolls up (closing §5.9 → §7.5).
- **files to modify:** `web/projects/core/src/analytics/analytics-emitter.service.ts`,
  small wiring edits in the consumer feature containers that originate events.
- **acceptance criteria:** Each consumer interaction emits exactly one correctly-shaped event whose
  `Kind` is one of the backend's eight kinds; **no flushed event ever carries a `geo` kind** (the
  geolocation signal appears only as `latitude`/`longitude` fields on a `search`/`session` event), so
  the ingest endpoint never 400s on an unknown kind; events batch and flush to the ingest endpoint;
  the session id is attached; no PII leaves the client; an ingest failure does not break the user flow
  (best-effort, non-blocking).
- **testing requirements:** Unit tests: emitter batches and flushes the correct payload per event
  kind; **every flushed event's `Kind` is in the eight-member union and none is `geo`** (and the
  geo-opt-in path produces a `search`/`session` event bearing `latitude`/`longitude`, not a `geo`
  event); a failed flush is swallowed and retried/dropped without throwing into the UI. Component:
  opening a card triggers a `card_open` flush.

### Step 17: i18n plumbing (Ukrainian-first, runtime catalogs, locale-aware formatting, language switch)
- **status:** [x] done
- **description:** Establish the **i18n module** with `@ngx-translate/core` (justification: runtime
  catalog swapping lets the wired language switch change locale without a rebuild, which compile-time
  `$localize` cannot do; catalogs live view-independently and all copy is catalog-keyed). Populate
  the **`uk`** catalog as the default and only launch locale; scaffold an empty `en` (or other EU)
  catalog to prove additive locales need no architectural change. Drive locale-aware number/
  **distance (km)**/**currency** formatting (currency by the backend `PriceCurrency`, not hardcoded
  ₴) through the formatters from Step 8. Wire the language-switch slot from Step 2 to switch locale
  and persist the choice. Ensure layouts tolerate longer EU translations.
- **files to modify:** `web/projects/core/src/i18n/*` (config, loader, catalog keys),
  `web/projects/app/src/assets/i18n/uk.json`, `web/projects/app/src/assets/i18n/en.json`
  (placeholder), language-switch component, `app.config.ts` provider, and replacing hardcoded copy
  across the feature templates with catalog keys.
- **acceptance criteria:** Default locale is `uk` and every user-facing string resolves from a
  catalog key (no hardcoded copy remains in templates — verified by a scan); switching the language
  swaps catalogs at runtime and persists; number/distance/currency format per locale and per
  `PriceCurrency`; adding a new locale is a catalog file only (no code change); longer translations
  do not break layout at phone width.
- **testing requirements:** Unit: language switch changes the active catalog and persists; formatter
  locale tests (km, currency by `PriceCurrency`, number grouping). Scan test/lint: no untranslated
  literal copy in feature templates.

### Step 18: Operator portal shell + role-gated lazy boundary (web-only, separate from consumer)
- **status:** [x] done
- **description:** Create **Surface B**, the web-only operator portal, as a **separately
  lazy-loaded** area with a clean boundary so **consumer code never depends on portal code** (and
  vice-versa) — enforced by the project/route structure. Gate the whole portal behind the
  `adminGuard` (Keycloak admin/operator role; admin host enforces the policy server-side too). Build
  the portal shell/nav and a feature-routing skeleton for the management features. Note in-code the
  **persona decision**: portal targets the admin/operator persona on the Admin host; owner self-serve
  is a future backend dependency (Risk #3) — structure features/role-gating so an owner persona can
  be added later without restructuring.
- **files to modify:** `web/projects/app/src/app/portal/portal.routes.ts`,
  `web/projects/app/src/app/portal/portal-shell.component.ts`, root route registration (lazy import),
  guard wiring.
- **acceptance criteria:** The portal is a lazy-loaded route tree gated by `adminGuard`; a non-admin
  token cannot reach it; the consumer bundle does not import portal modules and the portal does not
  import consumer feature internals (verified by a dependency/lint check); the portal shell renders
  its nav skeleton.
- **testing requirements:** Component/route tests: `adminGuard` blocks a user-only token and admits
  an admin token; a build/dependency check asserts the consumer↔portal import boundary.

### Step 19: Operator portal features — menu management, protection flags, address, photo permission
- **status:** [x] done
- **description:** Build four lazy-loaded **management features** wiring the Admin-host endpoints,
  each its own modular feature (1:1 blueprint): **menu management** (edit price/weight + re-categorize
  a menu item → `PUT /admin/menu-items/{id}`), **protection flags** (per-item `do-not-parse` →
  `PUT /admin/menu-items/{id}/do-not-parse`; restaurant `do-not-update` →
  `PUT /admin/restaurants/{id}/do-not-update` — admin > parser, invariant #3),
  **address management** (`PUT /admin/restaurants/{id}/address`, triggers server-side re-geocode,
  invariant #7), and **photo permission** (`PUT /admin/photos/{id}/permission` — generic photos stay
  the default elsewhere, invariant #8). All use the admin data-access operations, the shared loading/
  error pattern, and treat 204 as success.
- **files to modify:** `web/projects/app/src/app/portal/menu/*`,
  `web/projects/app/src/app/portal/protection/*`,
  `web/projects/app/src/app/portal/address/*`,
  `web/projects/app/src/app/portal/photos/*`, portal route registration.
- **acceptance criteria:** Each feature performs its admin write and reflects 204 success / 404/400
  failure via the typed error model; menu edit re-categorizes (changes `dishId`) and edits price/
  weight; protection toggles set the flags; address edit posts line+city; photo permission toggles
  the gate; copy reflects admin > parser and the generic-photo default; each feature is independently
  lazy-loaded and admin-gated.
- **testing requirements:** Component tests per feature: a successful write shows success and a 400/
  404 shows the typed error; the request body/method/URL match the endpoint (asserted via the data-
  access unit mocks). At least one e2e-able happy path is reachable in the portal.

### Step 20: Operator portal features — Verified subscription + labeled ad placements
- **status:** [x] done
- **description:** Build the **Verified** feature (grant Basic/Pro → `PUT /admin/venues/{id}/verified`
  with the tier in the body; revoke → `DELETE /admin/venues/{id}/verified`) and the **labeled ad
  placement** feature (create a targeted, time-bounded placement → `POST /admin/venues/{id}/ad-
  placements` with `{ targetingKey, startsAt, endsAt }`, returning 201 + id). The UI presents the
  Verified badge as a **partner/confirmed status, not a quality seal**, and presents the ad placement
  explicitly as a **paid, labeled slot separate from organic ranking** (invariant #10). No real
  billing UI beyond these management actions (payment is a server-side no-op seam).
- **files to modify:** `web/projects/app/src/app/portal/verified/*`,
  `web/projects/app/src/app/portal/ads/*`, portal route registration.
- **acceptance criteria:** Grant posts the chosen tier and reflects 204; revoke issues DELETE and
  reflects 204; ad-placement create posts targeting+window and reflects the 201+id; the Verified UI
  copy frames it as partner status (not a quality seal); the ad-placement UI frames it as a paid,
  labeled, separated slot; no payment-processor logic is present.
- **testing requirements:** Component tests: grant/revoke/create call the right endpoint with the
  right body and reflect success/failure; copy assertions for the partner-status and labeled-ad
  framing. Unit: ad-placement body maps `startsAt`/`endsAt` correctly.

### Step 21: Backend addition #1a — Ratings write port + Dapper adapter + Ratings.Application submit use-case
- **status:** [x] done
- **plan divergence (developer, Step 21):** The new `AddRatingsApplication(connectionString)` DI extension
  is placed in **`WhereToEat.Ratings.Infrastructure`** (DependencyInjection) rather than in the new
  `Ratings.Application` project. Reason: the extension must register the Infrastructure Dapper adapter +
  the MassTransit-backed publisher adapter; placing that registration in the Application project would make
  `WhereToEat.Ratings.Application` depend on `WhereToEat.Ratings.Infrastructure`, which the
  `Application_DependsOnNoInfrastructureAndNoHost` architecture fitness test forbids. This mirrors the
  existing `AddAdminModule` convention (the Admin module's composition extension lives in Admin.Infrastructure
  and wires the Application handlers too). `Ratings.Application` stays MassTransit/Infrastructure-free; the
  publish seam is the `IRatingGivenPublisher` port (Application) with a MassTransit adapter (Infrastructure).
- **description:** Add the **rating write side** that is currently missing (only the aggregate-rollup
  repository exists). In `WhereToEat.Ratings.Domain.Abstractions`, add an **`IRatingRepository`-style
  write port** that can **look up an existing `Rating` by user + restaurant** and **save or update a
  `Rating` fact** (find-then-revise-or-create), sitting alongside `IRatingAggregateRepository`
  without replacing it. Add its **Dapper Infrastructure adapter** over the existing `ratings.Rating`
  table (which already has `UQ_Rating_Restaurant_User` and the 1..5 check), reusing `RatingsSql`
  for new parameterized lookup/insert/update statements and the existing connection factory. Create a
  new **`WhereToEat.Ratings.Application`** project with a **submit-rating use-case/command handler**
  that, for an authenticated caller: maps the IdP `sub` to the rating's opaque `UserRef` (no PII —
  invariant #11), looks up any existing rating for (user, restaurant), **revises in place** (Domain
  `Revise`) or **creates** a new `Rating` (Domain `Create`, score validated 1..5), persists via the
  write port, and **publishes the existing `RatingGiven` integration event** (via the MassTransit
  publish seam) so the existing recompute job + cumulative all-time aggregate produce the **smoothed**
  value the recommend/details paths already display — nothing about how ratings are read or ranked
  changes (invariant #6).
  **DI placement (critical — keep the write path off the Worker host):** do **not** add the new write
  port + `SubmitRatingCommandHandler` to `AddRatingsPersistence`. That extension is called by the
  **Worker host** (`WorkerCompositionRoot` → `AddRatingsPersistence`), which only needs
  `IRatingAggregateRepository` + `IRatingAggregateRecomputer` for the aggregate-recompute job and has
  no use for the rating write path. Instead introduce a **separate `AddRatingsApplication(connectionString)`
  registration extension** (mirroring how the codebase keeps each module's parts independently
  registrable — e.g. Analytics' `AddAnalyticsPersistence` vs `AddAnalyticsModule`) that wires the new
  `IRatingRepository` write port + its Dapper adapter + the `SubmitRatingCommandHandler` use-case
  (sharing the existing connection factory via `TryAddSingleton`). The **Public host** calls
  `AddRatingsApplication` (Step 22); the Worker keeps calling the **unchanged** `AddRatingsPersistence`.
- **files to modify:** `src/Modules/Ratings/WhereToEat.Ratings.Domain/Abstractions/IRatingRepository.cs`
  (new write port), `src/Modules/Ratings/WhereToEat.Ratings.Infrastructure/Persistence/DapperRatingRepository.cs`
  (new adapter), `src/Modules/Ratings/WhereToEat.Ratings.Infrastructure/Persistence/RatingsSql.cs`
  (new select-by-user-restaurant / insert / update statements), new project
  `src/Modules/Ratings/WhereToEat.Ratings.Application/` (`SubmitRatingCommand.cs`,
  `SubmitRatingCommandHandler.cs`), a **new `AddRatingsApplication(connectionString)` DI extension**
  (e.g. `RatingsApplicationServiceCollectionExtensions.cs` in the new Application project) that
  registers the write port + Dapper adapter + the submit use-case handler;
  **`RatingsInfrastructureServiceCollectionExtensions.cs` (`AddRatingsPersistence`) is left unchanged**
  (Worker path); add the new project(s) to `src/WhereToEat.sln`.
- **acceptance criteria:** The new write port can look up a `Rating` by (user, restaurant) and save/
  update it; the Dapper adapter round-trips against `ratings.Rating` and respects the unique
  (Restaurant,User) constraint (revise updates the same row, never duplicates — one-rating-per-user-
  per-restaurant); the submit use-case maps `sub`→opaque `UserRef` with no PII stored, validates the
  score via the Domain (1..5), chooses revise-vs-create correctly, and publishes `RatingGiven`; the
  **new write port + `SubmitRatingCommandHandler` are registered only by the new
  `AddRatingsApplication(connectionString)` extension, and `AddRatingsPersistence` is unchanged** so
  the Worker host (which calls `AddRatingsPersistence`) gets no rating-write dependency; the existing
  aggregate/recompute path consumes it unchanged (read/ranking behavior untouched); the solution
  builds.
- **testing requirements:** Unit tests (mirroring `WhereToEat.Ratings.Domain.UnitTests` /
  `Admin.Application.UnitTests` conventions): submit creates a new fact when none exists and revises
  when one exists; score out of range returns the Domain validation failure; `RatingGiven` is
  published on success (publish seam mocked). Integration tests in
  `tests/Integration/WhereToEat.Ratings.Integration`: the Dapper adapter persists/updates and honors
  the unique constraint.

### Step 22: Backend addition #1b — authenticated Public-host POST rating endpoint + frontend rating UI wiring
- **status:** [x] done
- **description:** Add an **authenticated `POST` rating endpoint on the Public host** behind the
  existing `UserPolicy` (a valid Keycloak user token; `IIdentityContext.SubjectId` supplies the
  caller `sub`), driving the Step 21 submit-rating use-case. Map a request `{ score }` for a path
  restaurant id; return success (200/204) and the same `{ error, message }` 400/401 envelope on
  validation/auth failure. **Register the new `AddRatingsApplication(connectionString)` extension
  (from Step 21) in the Public host composition root (`WhereToEat.PublicApi/Program.cs`) — NOT
  `AddRatingsPersistence`** (that belongs to the Worker host; the Public host needs the write port +
  `SubmitRatingCommandHandler`, which only `AddRatingsApplication` registers). Then **wire the
  frontend rating UI**: add a `submitRating(restaurantId, score)` data-access operation (authed,
  bearer attached), a rating-submit component on details/results that requires sign-in, posts a 1..5
  score, and is **honest that the displayed value is the smoothed/cumulative rating** (updated on the
  next recompute, not the user's raw last score — invariant #6). **This step owns wiring the
  `rating_given` analytics event into the Step 16 batching emitter** (using the `rating_given` event
  builder from Step 8 and the `rating_given` `EventKind` member from Step 4): on a successful submit,
  the rating-submit component emits the `rating_given` event through the Step 16 emitter.
- **files to modify:** `src/Hosts/WhereToEat.PublicApi/Endpoints/RatingEndpoints.cs` (new),
  `src/Hosts/WhereToEat.PublicApi/Program.cs` (call **`AddRatingsApplication(connectionString)`** in
  the module-registration block + map the endpoint),
  `web/projects/core/src/data-access/public/rating.operation.ts`,
  `web/projects/app/src/app/consumer/rating/rating-submit.component.ts`, details/results wiring,
  the Step 16 analytics-emitter `rating_given` wiring + event-builder usage.
- **acceptance criteria:** The endpoint requires a valid user token (401 anonymous, 200/204 on
  success), maps `sub`→opaque user ref, validates score 1..5 (400 with `{error,message}` otherwise),
  drives the use-case, and publishes `RatingGiven` (via Step 21); the Public host wires the rating
  write path via **`AddRatingsApplication`** (named explicitly, **not** `AddRatingsPersistence`) in
  `WhereToEat.PublicApi/Program.cs`; the frontend submit attaches the bearer, posts a 1..5 score,
  blocks/redirects when not signed in, and clearly labels the shown rating as smoothed/cumulative; a
  `rating_given` event **is wired into the Step 16 emitter by this step** and emits on a successful
  submit. No change to how ratings are read or ranked.
- **testing requirements:** Backend integration tests in
  `tests/Integration/WhereToEat.PublicApi.Integration` (mirroring the existing `/me` auth tests):
  401 unauthenticated, 200/204 authenticated, 400 on bad score, `RatingGiven` published, revise vs
  new. Frontend: data-access unit test (method/URL/bearer/body), component test (submit posts score,
  sign-in gate, smoothed-label copy), and the rating-given analytics emission.

### Step 23: Backend addition #2 — analytics rollup-reader extension + admin-scoped read endpoint
- **status:** [x] done
- **description:** **Extend the `IAnalyticsRollupReader` seam** (today only impressions/card-opens/
  CTR per restaurant per hour) with additional **aggregate-only** queries to back the §7.5 dashboard
  metric families, every query returning grouped counts/ratios only (invariant #11): **visibility/
  traffic** (impressions, card-opens, CTR, action clicks), **demand by category/dish** (which
  selections are searched in an area, from the retained category/dish dimensions + coarse geohash),
  **price positioning** (the venue's prices vs the area/category median the engine already computes),
  **ratings distribution** (from the materialized ratings aggregate), and the **conversion funnel**
  (impression → card-open → action). Implement the new Dapper queries in
  `AnalyticsRollupReader`/`AnalyticsSql` grouping the append-only event store (and joining the
  existing price-median + ratings-aggregate materializations as needed) — **no actor hash, no per-
  event id, no precise coordinate, no per-user row** crosses the boundary. Expose an **authenticated,
  admin-policy-gated analytics read endpoint on the Admin host** (the admin-only policy —
  admin 200 / non-admin 403 / anonymous 401) returning those aggregate DTOs.
  **Host registration (the real gap):** `IAnalyticsRollupReader`/`AnalyticsRollupReader` are **already
  `public`** in the codebase, so **no visibility change is needed** (do not add any "make public /
  internal-test-only" wording). The actual gap is that the **Admin host does not register the
  analytics module today** — `WhereToEat.AdminApi/Program.cs` never calls any `AddAnalytics*`, so
  `IAnalyticsRollupReader` will not resolve there. Fix by adding a **slim analytics read registration**
  to the Admin host composition root: either call the existing `AddAnalyticsModule(...)`, or add a new
  read-only `AddAnalyticsReadModule(connectionString)` extension that registers just the connection
  factory + `IAnalyticsRollupReader → AnalyticsRollupReader` (without the ingest/anonymizer stack the
  Admin host doesn't need) — mirroring how the codebase keeps each module's parts independently
  registrable (e.g. `AddAnalyticsPersistence` vs `AddAnalyticsModule`). The Admin host is the correct
  host because the design routes the operator portal (Step 18) to the Admin host. `AnalyticsSql` stays
  `internal static`; the new grouped queries are added to that same `internal` class (no visibility
  change there either).
- **files to modify:** `src/Modules/Analytics/WhereToEat.Analytics.Application/IAnalyticsRollupReader.cs`
  (new aggregate methods + aggregate result records — the interface is already `public`),
  `src/Modules/Analytics/WhereToEat.Analytics.Infrastructure/Persistence/AnalyticsRollupReader.cs`
  + `AnalyticsSql.cs` (new grouped queries in the existing `internal static AnalyticsSql`),
  `src/Modules/Analytics/WhereToEat.Analytics.Infrastructure/DependencyInjection/AnalyticsInfrastructureServiceCollectionExtensions.cs`
  (add the slim `AddAnalyticsReadModule(connectionString)` read-only registration, unless reusing the
  existing `AddAnalyticsModule`), `src/Hosts/WhereToEat.AdminApi/Endpoints/AnalyticsDashboardEndpoints.cs`
  (new), `src/Hosts/WhereToEat.AdminApi/Program.cs` (**call the analytics read registration**
  `AddAnalyticsReadModule(connectionString)` / `AddAnalyticsModule(...)` in the module block + map the
  endpoint — the Admin host registers no analytics module today).
- **acceptance criteria:** The seam exposes the §7.5 metric families as **aggregates only** (counts/
  ratios) — by query shape no actor hash/per-event id/precise coordinate/per-user row can leave;
  the **Admin host registers the analytics read path** (via `AddAnalyticsReadModule` or
  `AddAnalyticsModule` in `WhereToEat.AdminApi/Program.cs`) so `IAnalyticsRollupReader` resolves there
  (it does not today); **no type-visibility change is made** (the reader interface/class are already
  `public`; `AnalyticsSql` stays `internal`); the endpoint is admin-gated (admin 200, non-admin 403,
  anonymous 401) on the Admin host; the responses are anonymized aggregate DTOs; existing minimal
  rollup behavior is preserved.
- **testing requirements:** Integration tests in `tests/Integration/WhereToEat.Analytics.Integration`
  for each new query (grouped counts correct, no identifying column selected) and in
  `tests/Integration/WhereToEat.AdminApi.Integration` for the endpoint (admin 200 / non-admin 403 /
  anonymous 401, aggregates-only shape). Mirrors existing analytics/admin test conventions.

### Step 24: Operator portal feature — analytics dashboard wired to the admin read endpoint
- **status:** [x] done
- **description:** Build the **analytics dashboard** portal feature covering the §7.5 metric families
  (visibility/traffic, demand by category/dish, price positioning, ratings distribution, conversion
  funnel), wired to the **new admin-scoped, aggregates-only** read endpoint (Step 23) via a new admin
  data-access operation. Every figure rendered is an aggregate the backend returns; the frontend
  never receives or reconstructs per-user/per-event data (invariant #11 end-to-end). Use accessible,
  responsive charts/tables themed from the design tokens; reuse the shared loading/error pattern.
- **files to modify:** `web/projects/core/src/data-access/admin/analytics-dashboard.operation.ts`,
  `web/projects/app/src/app/portal/analytics/*` (container + presentational metric components +
  route), portal route registration.
- **acceptance criteria:** The dashboard fetches and renders the §7.5 metric families from the admin
  endpoint; the data-access call attaches the bearer and targets the admin prefix; only aggregate
  values appear (no per-user/per-event fields exist in the typed response); the feature is admin-
  gated and lazy-loaded; charts/tables are accessible (labels, keyboard) and responsive.
- **testing requirements:** Data-access unit test (method/URL/bearer, aggregates-only typed
  response); component tests rendering each metric family from mock aggregates; a check that the
  typed response carries no per-user/per-event field. An e2e portal flow lands on the dashboard.

### Step 25: Accessibility, performance, and visual-polish hardening pass
- **status:** [x] done
- **reconcile note (developer, Step 25):** Resuming after a session-limit interruption. Pre-existing
  partial work found: `angular.json` `anyComponentStyle` budget already raised 4kB→6kB warn (8kB error
  kept) and `initial` warn 500kB→700kB — completing the justification + remaining hardening here.
- **description:** Cross-cutting hardening: audit **WCAG** (semantic landmarks, keyboard nav across
  selection/filter/result/map/dialog flows, focus management for modals/dialogs, screen-reader labels
  on controls and result cards, token-enforced contrast); audit **performance** (verify lazy routes,
  OnPush everywhere, taxonomy caching, debounced prefix search, long-list virtualization, and the
  optimized/lazy/responsive generic-photo strategy); and apply motion/polish from Material 3 +
  tokens for the on-trend European look. Fix gaps found.
- **files to modify:** targeted fixes across consumer/portal components and shared UI; possibly
  `_tokens.scss`/`_theme.scss` and image-asset pipeline config.
- **acceptance criteria:** Keyboard-only completion of the core consumer journey and a portal flow;
  no critical a11y violations in an automated audit (axe) on key screens; all routes lazy and all
  components OnPush; the taxonomy is cached and prefix search debounced; long lists virtualize;
  generic photos are optimized/lazy/responsive; AA contrast holds.
- **testing requirements:** Automated a11y (axe) checks on the selection, results, map-modal,
  details, and dashboard screens with zero critical violations; a performance check that routes are
  lazily loaded (bundle/route analysis) and that OnPush is the default.

### Step 26: Testing & e2e closeout (coverage gates + key journeys)
- **status:** [x] done
- **description:** Final test consolidation. Confirm **high unit coverage of the isolated business
  logic** (request builder, validation, formatters, analytics builders, signal stores) — the
  coverage that keeps the RN rewrite low-risk — and add any missing component tests for smart/dumb
  components. Author **Playwright e2e** for the key journeys: (1) build selection → choose match/sort/
  filters → recommend → results → view on map → details; (2) sign in → submit a rating (smoothed-
  label honesty) → see `rating_given` emitted; (3) admin sign-in → a portal management action →
  the analytics dashboard. Ensure the two backend additions remain covered by their server-side
  suites (Steps 21–23). Set a coverage threshold for `core` business logic in CI.
- **files to modify:** `web/e2e/*.spec.ts` (the three journeys), additional component/unit specs as
  needed, `web/karma.conf.js`/coverage config + CI threshold, `web/playwright.config.ts`.
- **acceptance criteria:** The `core` business-logic suite meets the configured coverage threshold;
  the three e2e journeys pass against the dev gateway (public reads anonymous; rating-submit and
  portal/dashboard authenticated); the backend-addition suites pass; `build`+`test`+`lint`+`e2e`
  are green in CI.
- **testing requirements:** Unit coverage gate enforced in CI for `core`; three green Playwright
  journeys; backend `dotnet test` green for the Ratings/PublicApi/Analytics/AdminApi suites touched
  by Steps 21–23.
```

## Notes on coverage & traceability

- **Every backend endpoint is wired** with one data-access operation: the 10 public (`/categories`,
  `/categories/{id}/dishes`, `/restaurants/{id}`, `/search/categories`, `/search/dishes`,
  `/recommend`, `/map/{restaurantId}`, `/analytics/events`, `/me`, `/health`) and 8 admin
  (`menu-items` edit, `address`, `do-not-parse`, `do-not-update`, `photos permission`, `verified`
  grant/revoke, `ad-placements`) are introduced in **Step 5** and consumed in Steps 10–16, 19–20,
  plus the two new endpoints (rating-submit Step 22, analytics-dashboard Step 23/24).
- **Contract key values** are pinned in **Step 4** (`match: or|and`;
  `sort: price|distance|rating|price-quality|best`; `filters: price|rating`) and exercised by the
  request builder (Step 8) and query controls (Step 11).
- **Backend additions** are Steps 21–23 (rating write port + adapter + Application use-case;
  authenticated Public POST endpoint; analytics rollup-reader extension + admin read endpoint), each
  grounded in the real modules and honoring invariants #6 and #11; their frontend wiring is Steps 22
  and 24.
