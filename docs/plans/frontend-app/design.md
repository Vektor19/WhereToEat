# Design — frontend-app

> High-level design for the **WhereToEat / ДеПоїсти** frontend: an Angular workspace that
> delivers (A) the consumer search experience and (B) the restaurant/operator management
> portal, wired to the existing two-host .NET backend. This document is the source of truth
> for the design; exact file paths, imports, and step-by-step work belong to `implementation.md`.
>
> **Scope note:** the bulk of this plan is the Angular frontend, but two product capabilities — the
> rating write path (§5.6) and the B2B analytics dashboard (§7.5) — are delivered **fully**, which
> requires a **scoped set of backend additions** (an authenticated rating-submit endpoint + its thin
> use-case on the Public host, and an admin/operator-scoped, aggregates-only analytics read endpoint
> + a rollup-reader extension on the Admin host). These are described as capabilities/contracts here,
> not implementations. Restaurant-owner self-serve / claim-verify is the one product gap left as a
> **future** backend dependency (see Risks #3).
>
> Every decision below is checked against the CLAUDE.md iron invariants — including the new backend
> additions, which must honor invariant #6 (our-own, cumulative, smoothed ratings) and invariant #11
> (analytics is aggregates-only, never per-user/per-event). The frontend never works around an
> invariant — it renders what the backend guarantees (deterministic search, our-own ratings/geo,
> always-free contact links, labeled ads, minimal disclaimers, privacy).

## Goals

1. **Wire every existing backend endpoint** to the frontend and use it. Public host: list
   categories, list dishes per category, deterministic prefix search for categories/dishes,
   restaurant details (with always-present contact links), `recommend`, map payload, analytics
   ingest, and the authenticated `/me` probe. Admin host: menu-item edit/re-categorize,
   address edit (re-geocode), `do-not-parse` / `do-not-update` protection flags, real-photo
   permission, Verified grant/revoke, and labeled ad-placement creation.
   **Plus a scoped set of new backend additions this plan delivers (see Goal 7):** an authenticated
   rating-submit endpoint on the Public host and an admin/operator-scoped, aggregates-only analytics
   read endpoint on the Admin host, each with a thin application use-case behind it. The bulk of the
   plan remains the Angular frontend; these two backend additions are the minimum needed to deliver
   the rating write path (§5.6) and the B2B analytics dashboard (§7.5) **fully**, not provisionally.
2. **Two clearly separated surfaces in one workspace:** (A) a **consumer search app** whose clean,
   well-bounded architecture is the structural blueprint for a future React Native mobile app, and
   (B) a **web-only restaurant / operator management portal** built as a modular, feature-based system.
3. **Structural portability to React Native as a primary design driver.** The app uses **Angular
   fully and idiomatically** (standalone components, signals + RxJS where each fits, dependency
   injection and services, the router, the HttpClient stack, a modern component/design system) —
   full Angular usage is a requirement, justified by maintainability, polish, and staying current.
   Portability is therefore achieved through **clean architecture, not framework-free code reuse**:
   by writing the frontend in a DRY, SOLID, well-patterned way with **clear feature/module
   boundaries and a clean separation of business logic from view**, the equivalent modules and
   logic (recommendation request building, validation, formatting, domain models, analytics event
   builders, Haversine display math, i18n catalogs) can be **readily re-implemented (rewritten) in
   React Native with minimal cognitive translation** — mirrored module-for-module rather than copied
   verbatim. It is expected that this business logic uses Angular DI/services and TypeScript idioms;
   the portability value comes from the cleanliness, naming, and isolation of that logic, not from
   avoiding the framework.
4. **Ukrainian-first, localization-ready, mobile-first audience.** The app **ships with Ukrainian
   (`uk`) as the default and only launch locale**, but the full i18n / localization plumbing is
   built from day one so other European languages can be added later **with no architectural
   change**: message catalogs live in a dedicated, view-independent i18n module, all user-facing
   strings are catalog-keyed (no hardcoded copy), number / distance (km) / currency formatting is
   locale-aware, and a language
   switch is wired even though only `uk` is populated at launch. Adaptive responsive layout
   (phone-first); money is formatted by the per-menu-item / per-restaurant `PriceCurrency` the
   backend returns (₴ the expected default for the Ukrainian launch, but **not** hardcoded as the
   only currency — the UI formats by the returned `PriceCurrency`), WCAG-aligned accessibility, and
   a harmonious design-token color system. The layout tolerates longer European translations so the
   later locale additions do not break it.
5. **Maintainable, scalable, robust.** Feature-based modules, lazy-loaded routes, smart/dumb
   (container/presentational) component separation, a single typed data-access layer mapping each
   endpoint, an explicit Angular-idiomatic reactive **signal-based state strategy** kept cohesive
   and well-separated, consistent loading/error handling, and good runtime performance. SOLID and
   DRY are honored by layering and shared abstractions, not duplication — and that same cleanliness
   is what makes each module cheap to mirror in React Native later.
6. **Faithful to the product invariants** in every flow: deterministic selection-based search
   (no NLP, no free-text query to the engine), two-level taxonomy Category → Dish, explicit
   sort dominates with coverage as tie-breaker, our smoothed rating with a low-review note,
   inline live "View on map" Google Maps Embed (nothing cached), always-free contact links,
   generic dish photos by default, opt-in approximate geolocation, anonymized analytics
   emission, clearly-labeled and visually-separated ad slots, and minimal/discreet disclaimers.
7. **Deliver the two product gaps fully via scoped backend additions.** The rating write path and
   the B2B analytics dashboard are **built fully, not deferred**. This requires a small, scoped set
   of backend additions, described here as capabilities/contracts:
   - **Rating submit (Public host, authenticated).** A submit-rating application use-case that
     persists one our-own `Rating` fact for the authenticated user and publishes the existing
     `RatingGiven` integration event, plus an authenticated `POST` rating endpoint that drives it.
     The existing recompute job + cumulative all-time aggregate then feed the **smoothed** rating
     that `recommend`/details already display — so the write path closes the loop without changing
     how ratings are read or ranked. Honors invariant #6 (our-own, cumulative all-time, smoothed)
     and invariant #11 (no PII — only the opaque user/restaurant references and the score).
   - **Analytics read (Admin host, admin/operator-scoped, aggregates-only).** An authenticated,
     admin-policy-gated analytics read endpoint that returns **aggregates only** (invariant #11 —
     never per-user or per-event rows), backed by an extended rollup reader that surfaces the §7.5
     dashboard metric families (visibility/traffic, demand by category/dish, price positioning,
     ratings distribution, conversion funnel) as anonymized aggregates.

## Non-Goals

- **No React Native / mobile app is built here.** This plan is frontend (web) only. The mobile
  app is a *future* parallel build; we keep the Angular architecture clean and well-bounded so the
  RN team can mirror our modules module-for-module, but we do not implement it (and we do not ship a
  framework-free "core" intended for literal cross-framework reuse).
- **Backend changes are limited to a scoped set of additions (not a blanket "no backend work").**
  This plan **does** add two backend capabilities — the authenticated rating-submit endpoint +
  its thin submit-rating use-case (Public host) and the admin/operator-scoped, aggregates-only
  analytics read endpoint + its rollup-reader extension (Admin host) — described in Goal 7 and
  the Risks. **Beyond those two additions, we change nothing server-side:** every other capability
  is consumed as it stands. **Restaurant-owner self-serve / claim-verify remains a future backend
  dependency and is explicitly out of scope** (Risk #3) — the portal still targets the
  admin/operator persona. No existing read or ranking behavior is modified; the rating write path
  reuses the existing aggregate/recompute/`RatingGiven` machinery rather than touching how ratings
  are read or ranked.
- **No restaurant-search/recommendation features in the portal.** The portal is management
  (operator/admin); consumer search lives only in surface (A).
- **No identity provider implementation.** The IdP is **Keycloak (self-hosted)**; the backend is
  an OIDC resource server that validates Keycloak-issued tokens (JWKS) and the frontend integrates
  with Keycloak via standard OIDC Authorization Code + PKCE. We **configure and integrate with**
  Keycloak (authority, client, redirect URIs, scopes as environment config) but do not build or
  customize the IdP itself.
- **No real billing / payments UI logic beyond what the admin endpoints expose.** Verified and
  ad placements are management actions against existing endpoints (payment runs through a no-op
  seam server-side); we render those actions, not a payment processor.
- **No "updated X days ago" UI, no per-item price/photo disclaimers, no caching of Google
  ratings or coordinates** (these are forbidden by the invariants; calling them out as
  non-goals keeps the UX honest).
- **No design-token / visual brand finalization** beyond defining the token system and a
  harmonious palette structure; exact brand hex values can be tuned later without architectural
  impact.

## Overview

This plan spans **a single Angular frontend workspace plus a scoped set of backend additions**.
The overwhelming bulk is the frontend; the backend work is two thin, contract-level additions
(detailed in Risks #1 and #2) needed so two product capabilities — the rating write path and the
B2B analytics dashboard — ship **fully** rather than as disabled stubs:

- **Rating-submit capability (Public host).** A thin submit-rating application use-case +
  an authenticated `POST` rating endpoint, reusing the existing Ratings domain, aggregate/recompute
  job, and `RatingGiven` event so the smoothed rating the read paths already show simply gains a
  write source.
- **Analytics read capability (Admin host).** An admin/operator-scoped, aggregates-only analytics
  read endpoint + an extension of the existing rollup-reader seam to cover the §7.5 metric families,
  every metric kept anonymized/aggregate-only.

The frontend itself is a single Angular **workspace** organized into clean, layered **business-logic
modules** plus **application surfaces**, all written in idiomatic Angular:

- A **business-logic layer (Angular services / façades)** encapsulating the domain with minimal
  incidental view coupling: domain models and enums mirroring the backend contracts; a typed
  data-access layer (one operation per endpoint, defined behind interfaces so the HTTP
  implementation is injectable/swappable) built on Angular's `HttpClient`; the
  selection/recommendation **request builder** that turns category/dish selections + match + sort +
  filters into the exact `recommend` request shape; formatting/label helpers (currency, distance,
  smoothed-rating presentation including the low-review note); validation mirroring the backend's
  "exactly one of categoryId/dishId" and `userGeo` range rules so the UI fails fast before a
  round-trip; Haversine *display* helpers; analytics **event builders** producing the exact ingest
  payloads; and i18n message catalogs. These are plain, well-named, injectable services/pure
  functions — clean enough that an RN developer can mirror each one module-for-module, even though
  they freely use Angular DI and TypeScript idioms.
- A **typed data-access layer (Angular `HttpClient`)**: injectable services that implement the
  endpoint operation interfaces, attach bearer tokens, target the correct logical-host route prefix,
  and feed the reactive state services. Dependency inversion keeps feature code depending on the
  operation interfaces rather than on transport details, so the HTTP implementation can be swapped
  (and the RN equivalent re-implemented against the same interfaces).
- **Surface (A) — Consumer search app:** the deterministic selection flow (browse categories →
  dishes, or prefix-search to add a dish/category to the selection), match (Or/And) and sort
  modes, composable filters, the ranked results list (basket price / smoothed rating / distance
  / coverage), inline live "View on map" Embed, restaurant details with always-free contact
  links, rating **display and a fully-wired authenticated submit path** (posting to the new
  rating endpoint — see Risks #1), opt-in geolocation, and analytics emission. Its business logic
  lives in injectable services/signal stores with a clean boundary to the view; that boundary is the
  seam an RN rewrite mirrors, while the Angular components/templates themselves are the part that
  does *not* travel.
- **Surface (B) — Restaurant / operator management portal (web-only):** a modular, feature-based
  system. Each management capability (menu management, protection flags, photo permission,
  Verified subscription, labeled ad placements, and a **fully-wired analytics dashboard** backed
  by the new aggregates-only analytics read endpoint) is its own lazy-loaded feature module wiring
  the corresponding **Admin-host** endpoints, gated by Keycloak role/policy (the admin/operator
  role).

**Single origin via a reverse proxy / API gateway; two logical hosts behind it.** The backend is
two separate processes (Public API and Admin API), but the SPA talks to **one origin**: a reverse
proxy / API gateway sits in front of both hosts and the SPA uses **relative `/api/...` paths** in
every environment. CORS is therefore handled by infrastructure (the gateway), **not** by per-host
backend CORS config — which matters because neither host registers CORS today. The data-access
layer still distinguishes the **two logical hosts** by route prefix behind the gateway (a public
prefix vs an admin prefix): consumer features target the public routes (mostly anonymous), portal
features target the admin routes (all admin-policy-gated, bearer required), and the new
rating-submit endpoint is an authenticated public route. The same Keycloak token is accepted by
both hosts; the Admin host additionally enforces an admin-only policy, so a non-admin token is
rejected there. The gateway is an **infrastructure dependency**: in development it is the Angular
dev-server proxy or a local gateway; in production it is the deployed gateway. The data layer
never hardcodes cross-origin hosts — it composes relative prefixes from environment config.

## Key Components & Flows

### Layered architecture (maintainability, scalability, SOLID/DRY)

- **Domain & business logic (Angular services / façades).** One source of truth for the shapes
  the backend returns and accepts. The recommendation request builder, validation, formatting, and
  analytics builders are well-named injectable services / pure functions — independently
  unit-testable and isolated from the view. This is where DRY lives: a single place defines "how a
  selection becomes a request," "how a smoothed rating is presented," "how a distance is formatted,"
  etc. They use Angular DI and TypeScript idioms freely; their cleanliness and isolation are what
  make each one a 1:1 blueprint for the RN rewrite.
- **Typed data-access layer (Angular `HttpClient`, interface-fronted).** Dependency inversion:
  features depend on the *interface* of each endpoint operation, not on a concrete HTTP service. The
  Angular implementation chooses the **logical-host route prefix** (public vs admin, both relative
  to the single gateway origin), attaches the token, and translates transport errors into a typed
  error model. Because each operation is interface-fronted, the implementation is swappable and the
  RN app re-implements the same interface set. Every endpoint in the mapped surface — including the
  new authenticated rating-submit and admin analytics-read operations — has exactly one operation
  here.
- **State management (Angular-idiomatic, signal-based).** Selection/request state, results state,
  and the catalog cache live in cohesive, well-separated, **signal-based state services** — a
  signal-store pattern (NgRx SignalStore is a reasonable option, or a lightweight hand-rolled
  signal store) exposing readable signals plus explicit, testable state-mutating methods. State is
  reactive and integrates natively with Angular change detection and on-push components. Rationale:
  signals are the current Angular-idiomatic reactive model, keep state ergonomic, polished, and
  testable, and — because each store is cohesive and cleanly separated from the view — its shape and
  transitions map directly onto the equivalent RN state container, making the rewrite cheap without
  forcing the web app to forgo Angular conveniences and devtools.
- **Feature modularity & routing.** Both surfaces use **standalone components** and **lazy-loaded
  routes** per feature, so the portal's heavier management features and the consumer app load
  independently. Smart (container) components inject the state services and own coordination; dumb
  (presentational) standalone components are pure inputs/outputs — this keeps presentational
  components reusable and makes the smart/dumb seam align with the business-logic-vs-view boundary,
  which is exactly the seam an RN rewrite mirrors.
- **Performance.** Lazy routes; on-push change detection as the default; cache the rarely-changing
  taxonomy (categories/dishes) in a signal-based catalog store; debounce prefix-search input
  (deterministic anchored lookups, still no NLP); virtualize/lazy-render long result lists; and an **image
  strategy for generic dish photos** — generic category photos are *our own content* and the
  default on every dish (invariant #8), served as optimized, lazy-loaded, responsive assets,
  with real photos shown only where the backend signals permission.
- **Cross-cutting (Angular interceptors / functional providers).** A token-attachment interceptor
  (only on calls that need auth — the portal calls, the rating-submit call, and the analytics-read
  call), a host-routing concern (public vs admin **route prefix** behind the one gateway origin), a
  consistent error-normalization interceptor (mapping backend `{ error, message }` / 400 / 401 / 403
  / 404 to a typed error model and to user-facing localized messages), and a correlation/loading
  concern for UX state.
- **Environment config.** The gateway origin and the public/admin route prefixes, the Keycloak
  authority/client/redirect/scope config, the **map-provider selection and Google Maps Embed
  credential**, and feature flags live in environment configuration — never hardcoded. Because the
  SPA uses relative `/api/...` paths, the "origin" is typically empty in production (same-origin)
  and the dev proxy target in development.

### Consumer search flow (Surface A) — grounded in the product

1. **Build a selection (deterministic, invariant #1 & #2).** The user either browses the
   two-level taxonomy (list categories → list dishes within a category) or uses the **anchored
   prefix search** for categories/dishes to add items to a **selection list**. There is no
   free-text query to the engine — input only filters lists by prefix. Each selected item is
   exactly one of a category or a dish.
2. **Choose match & sort.** Match is Or / And (And = "combo," all selected present). Sort is an
   explicit single-field mode (price / distance / rating) **or** a composite mode
   (price-quality / best). The UI must make clear that explicit sort dominates and coverage is
   only a tie-breaker (invariant #5) — e.g., results are ordered by the chosen field, and
   coverage ("3 of 3") is shown as secondary information, never as the primary ordering.
3. **Apply composable filters.** Price (max) and rating (min) today; the filter UI is built so
   new composable filters (open-now, vegan, delivery, …) can be added without redesign
   (invariant #4).
4. **Request & render results.** The request builder produces the exact `recommend` body; the
   results list renders per card: basket price (≈ approximate, per the discreet ≈/ⓘ notice —
   not a per-item disclaimer, invariant #12/§10), our **smoothed rating** with a discreet
   low-review-count note (invariant #6), distance in km (when geolocation was provided), and
   coverage as secondary info. **Ad slots, when present, are clearly labeled and visually
   separated from organic results** (invariant #10) — though the current backend recommend
   response has no ad slot field, so this is a presentation contract the UI is structured to
   honor when the backend adds it.
5. **Inline "View on map" (§5.7), via a swappable map-provider interface.** Each card has a
   "View on map / Глянути на карті" affordance that fetches the map payload and opens a **live map**
   inline (modal/expansion). The map source sits behind a **map-provider interface** (so it is
   swappable and portable to React Native): the default provider is the **Google Maps Embed**, with
   its credential read from environment config; **until an Embed credential is present the provider
   degrades to a PlaceId / Maps deep-link** affordance instead of failing. Either way the §5.7
   semantics hold — nothing cached, no Google rating/coords stored; the Google rating is visible only
   inside the live Embed. A restaurant without stored coordinates shows the well-defined "no map
   data" state.
6. **Restaurant details (§5.8).** Always shows the **contact links (site/social) for free**,
   even for non-Verified venues; shows the menu with generic photos by default.
7. **Rating (§5.6) — display and a fully-wired submit path.** Display the smoothed rating and count
   everywhere the recommend/details data provides them. The submit path is **real, not provisional**:
   a signed-in user (a valid Keycloak user token; the `/me` seam already proves the auth path) posts
   a 1..5 score for a restaurant to the **new authenticated rating endpoint** (Risk #1). The backend
   persists one our-own `Rating` fact and publishes `RatingGiven`; the existing recompute job folds
   it into the cumulative all-time aggregate, so the **smoothed** value shown across the app updates
   on the next recompute (no per-rating live recalc, by design). The UI is honest that the displayed
   rating is smoothed/cumulative, not the user's raw last score.
8. **Geolocation (opt-in, invariant #11).** Distance ranking is on by default *only when* the
   user has opted in to approximate location; the user can decline or disable it to range
   further for price/quality. Location is approximate and optional; precise coordinates are
   never required.
9. **Analytics emission (§5.9 / §8.1).** The consumer flow emits the raw event batch
   (impression / view / card_open / action / search / filter / geo / session, and **rating_given**
   — now that the rating write path is in scope) via the analytics ingest endpoint. The frontend
   uses a session-scoped id for these events; the backend anonymizes server-side (invariant #11).
   These emitted events are the very signals the new admin analytics endpoint later rolls up into
   aggregates (closing the §5.9 → §7.5 loop). Event builders live in the business-logic layer as
   isolated, well-named services — clean enough to mirror in the React Native app.

### Restaurant / operator portal (Surface B) — modular, feature-based (web-only)

Each capability is an independent, lazy-loaded feature wiring **Admin-host** endpoints, gated by
Keycloak authentication + the admin/operator role:

- **Menu management** — edit price / weight and re-categorize a menu item (admin > parser,
  invariant #3).
- **Protection flags** — toggle per-menu-item `do-not-parse` and restaurant-level
  `do-not-update` (protect hand-curated data from the parser, invariant #3).
- **Address management** — edit the address (triggers re-geocode server-side, invariant #7).
- **Photo permission / real photos** — toggle the real-photo permission gate; generic photos
  remain the default elsewhere (invariant #8).
- **Verified subscription** — grant (Basic / Pro) and revoke Verified for a venue (the partner
  status; bd badge is a partner marker, not a quality seal — invariant #10).
- **Labeled ad placements** — create a targeted, time-bounded ad placement; the UI presents it
  explicitly as a paid, labeled slot separate from organic ranking (invariant #10).
- **Analytics dashboard (§7.5) — fully wired.** Built as a feature covering the §7.5 metric
  families (visibility/traffic, demand by category/dish, price positioning, ratings distribution,
  conversion funnel — all aggregated/anonymized) and wired to the **new admin/operator-scoped,
  aggregates-only analytics read endpoint** (Risk #1/#2). Every figure the dashboard renders is an
  aggregate the backend returns; the frontend never receives or reconstructs per-user or per-event
  data, preserving invariant #11 end-to-end. Because the current backend only exposes a minimal
  impressions/card-opens/CTR rollup, the richer metric families are backed by **additional
  aggregate queries that are part of this scope** (extending the rollup-reader seam) — see Risk #2.

**Persona decision (Risks #3).** There is no restaurant-owner self-serve API today; all
management actions live under the **admin-only** policy on the Admin host. Therefore this
portal targets the **admin/operator persona** and wires the existing admin endpoints. True
owner self-serve and claim/verify are treated as a **future backend dependency**: the portal's
feature structure and role-gating are designed so an owner persona and owner-scoped endpoints
can be added later without restructuring. We wire what exists and flag what does not.

### Cross-cutting: auth, hosts, i18n, accessibility, testing

- **OIDC Authorization Code + PKCE against Keycloak (self-hosted).** The access token is attached as
  a bearer on calls that require it. Consumer reads are anonymous (no token); the **rating-submit
  call requires a valid user token**, and the portal + analytics-read calls require an admin/operator
  token. The admin/operator **role used for portal/analytics gating comes from Keycloak** (realm/
  client roles carried in the token); the data layer attaches the bearer to the Public host
  (rating-submit) and the Admin host (portal + analytics), and the Admin host additionally enforces
  the admin policy server-side (non-admin token → 403). Authority, client id, redirect URIs, and
  scopes are environment config.
- **Single gateway origin, two logical route prefixes** are first-class config; the data layer routes
  each operation to the correct prefix (public vs admin) behind the one origin, and the SPA uses
  relative `/api/...` paths so there is no per-host CORS to manage in the app.
- **i18n / localization (Ukrainian-first, localization-ready):** message catalogs in a dedicated,
  view-independent i18n module with `uk` as the default and only populated launch locale; all copy
  catalog-keyed; locale-aware currency (₴ for launch, formatted by the backend's `PriceCurrency`),
  number, and distance (km) formatting; a language switch wired for future locales; layout that
  tolerates longer European translations; language selection persisted.
- **Accessibility (WCAG):** semantic structure, keyboard navigation, focus management for the
  map modal and dialogs, sufficient contrast enforced by the design tokens, and screen-reader
  labels for the selection/filter controls and result cards.
- **Modern component / design system (for a "pretty and trending" UI).** A modern, well-supported
  Angular component/design system — **Angular Material (Material 3) with custom design tokens /
  theming** as the recommended default, or a comparably modern, well-maintained component library —
  is layered on the harmonious design-token system below. Rationale: a current Material-3 component
  set gives mobile-first, accessible, polished UI out of the box for the European audience, keeps the
  look on-trend, and is the most maintainable path (large ecosystem, theming via tokens, motion and
  a11y built in). The choice is adjustable later without architectural impact because components
  consume design tokens rather than library-specific values.
- **Design tokens & color harmony:** a token system (color, spacing, typography, radius, elevation)
  drives a harmonious, accessible palette and feeds the component library's theme; components consume
  tokens, not raw values, so theming, motion/polish, and brand tuning are centralized and DRY.
- **Testing strategy:** the business-logic services are covered by **unit tests** (request builder,
  validation, formatting, analytics builders, state services) via Angular's testing utilities/DI;
  smart/dumb components by **component tests**; key journeys (build selection → recommend → view on
  map → details; submit a rating; and a portal management + analytics-dashboard flow) by **e2e**. The
  two new backend additions are covered server-side by their own tests — the submit-rating
  use-case/endpoint (auth 401/200, score validation, fact persisted, `RatingGiven` published,
  revision vs new) and the analytics read endpoint (admin 200 / non-admin 403, and the
  aggregates-only shape) — consistent with the existing backend's test conventions. High unit
  coverage of cleanly isolated business logic is what keeps each module a low-risk, faithful
  blueprint for the React Native rewrite.

## Key Decisions & Trade-offs

1. **Full, idiomatic Angular with portability through clean architecture (not a framework-free
   core).** The app uses Angular fully — standalone components, signals (plus RxJS where it fits),
   DI/services, the router, the HttpClient stack, and a modern component/design system — because that
   is what makes the system maintainable, polished, and current. Portability to React Native is
   achieved by writing the business logic in a DRY, SOLID, well-patterned way with clear feature/
   module boundaries and a clean separation of business logic from view, so the equivalent modules
   can be **rewritten** in RN module-for-module with minimal cognitive translation. *Trade-off:*
   disciplined layering and a strict business-logic-vs-view boundary add some upfront structure
   versus scattering logic through components; we get no literal cross-framework code reuse. Accepted:
   the user requires full Angular usage for maintainability and polish, and structural portability via
   cleanliness (not framework avoidance) is the stated goal — and SOLID/DRY are served either way.
2. **Angular-idiomatic, signal-based state services rather than a framework-free pub/sub store.**
   Selection/results/catalog state lives in cohesive, well-separated **signal-based state services**
   (a signal-store pattern — NgRx SignalStore is a reasonable option, or a lightweight hand-rolled
   signal store — exposing readable signals plus explicit, testable mutators). State integrates
   natively with Angular change detection and on-push components. *Trade-off:* the state is now
   Angular-native rather than literally liftable; we rely on each store being cohesive and cleanly
   separated so its shape and transitions map 1:1 onto the RN container. Accepted: signals are the
   current Angular-idiomatic reactive model, keep the state ergonomic, polished, and testable, and the
   clean separation is exactly what makes the RN rewrite of each store cheap.
3. **Single workspace, two surfaces, sharp boundary.** Consumer app and portal share the
   business-logic modules, data-access layer, and design system but are separately lazy-loaded and
   independently routable. *Trade-off:* a monorepo-style workspace is slightly more configuration
   than a single app. Accepted: it enforces the boundary the user asked for and keeps the consumer
   surface's business logic cleanly bounded — both so it does not depend on web-only portal code and
   so it remains the well-isolated blueprint the React Native rewrite mirrors.
4. **Portal targets admin/operator persona on the Admin host (wire what exists).** Avoids
   inventing an owner self-serve API. *Trade-off:* not yet a true restaurant-owner experience;
   owner self-serve and claim/verify are deferred to a backend dependency. Flagged explicitly so
   it is a conscious decision, not a silent gap.
5. **Rating-submit and the analytics dashboard are delivered fully via two in-scope backend
   additions, not provisional stubs.** Rather than mock these two paths, the plan adds the minimum
   backend each needs: a thin submit-rating use-case + authenticated `POST` rating endpoint (Public
   host), and an admin/operator-scoped, aggregates-only analytics read endpoint + a rollup-reader
   extension (Admin host). Both reuse existing machinery — the rating path rides the existing
   aggregate/recompute/`RatingGiven` flow; the analytics path extends the existing
   `IAnalyticsRollupReader` seam — so the additions stay small and do not alter read/ranking
   behavior. *Trade-off:* the plan's scope now spans backend as well as frontend, and the backend
   additions must be designed to honor invariants #6 (our-own/cumulative/smoothed ratings) and #11
   (aggregates-only analytics, no PII). Accepted: the user requires both capabilities to ship fully,
   and the additions are deliberately the thinnest contracts that achieve that. **Owner self-serve /
   claim-verify is the one capability deliberately left out of scope** (Decision #4 / Risk #3).
6. **Deterministic prefix-search typeahead, debounced — never NLP.** Honors invariant #1: input
   filters lists by anchored prefix only; the engine receives explicit ids. *Trade-off:* no
   "natural language" convenience, by design and by invariant.
7. **Explicit sort dominates in the UI; coverage is secondary.** The results presentation makes
   the ranking field primary and coverage a tie-breaker/secondary detail, matching invariant #5
   and the backend's ordering. *Trade-off:* coverage ("3 of 3") is intentionally de-emphasized
   even though users might expect it to dominate — this is correct per the product.
8. **Minimal, discreet disclaimers (invariant #12 / §10).** A single ≈/ⓘ data-accuracy notice
   (footer / info affordance), no "updated X days ago," no per-dish photo or per-price
   disclaimer. *Trade-off:* less explicit hedging on each element; accepted as the product's
   deliberate UX stance.
9. **Generic photos as optimized first-class assets; real photos gated by backend permission.**
   Generic category images are our content and the default everywhere (invariant #8); the UI
   only swaps in real photos where the backend says permission was granted.
10. **Keycloak (self-hosted) as the IdP, single gateway origin for the SPA.** Auth is OIDC
    Authorization Code + PKCE against Keycloak; the admin/operator role for portal/analytics gating
    is a Keycloak realm/client role read from the token (the backend already defaults to a
    Keycloak-style `admin`/`user` role mapping). The SPA talks to **one origin** behind a reverse
    proxy / API gateway and uses relative `/api/...` paths, so CORS is an infra concern, not per-host
    backend config. *Trade-off:* a gateway is an infrastructure dependency to stand up in each
    environment (dev proxy / local gateway; prod gateway), and Keycloak realm/role/claim specifics
    must be confirmed (Risk #4). Accepted: it keeps the SPA origin-agnostic and the backend hosts
    free of bespoke CORS, while the data layer still cleanly separates the two logical hosts.
11. **Map source behind a swappable map-provider interface; Google Embed credential from config.**
    The "View on map" source is an interface with a Google-Maps-Embed default whose credential is
    environment config; with no credential it degrades to a PlaceId / Maps deep-link rather than
    breaking. *Trade-off:* one layer of indirection over calling the Embed directly. Accepted: it
    keeps §5.7 semantics intact (live, nothing cached, no Google rating/coords stored), keeps the map
    source a clean interface that the React Native app re-implements with its own provider, and avoids
    a hard dependency on a key being present.
12. **Modern Angular component / design system for a "pretty and trending" UI.** Layer a modern,
    well-maintained Angular component system — **Angular Material (Material 3) with custom design
    tokens / theming** recommended as the default, or a comparably modern library — on the harmonious
    design-token color system, retaining motion/polish and accessibility. *Trade-off:* a dependency on
    a component library and its theming model versus fully bespoke components. Accepted: it delivers a
    mobile-first, accessible, on-trend look for the European audience with the least maintenance burden,
    and because components consume design tokens rather than library internals, the choice is adjustable
    later without architectural impact.

## Risks & Open Questions

### In-scope backend additions (now deliverables, not deferred — see Goal 7)

1. **Rating-submit endpoint + use-case + rating write port (Public host).** The Ratings module
   already has a Domain (the `Rating` fact with a 1..5 score, an opaque restaurant + user reference,
   a `Revise` path, and no PII per invariant #11) and an Infrastructure layer (the `ratings.Rating`
   fact table, the `IRatingAggregateRepository`/`IRatingAggregateRecomputer`, the worker recompute
   job, and the `RatingGiven` integration event). What is **missing** is the entire write side: there
   is **no write port for individual `Rating` facts** (only the aggregate-rollup repository
   `IRatingAggregateRepository` exists — it reads/aggregates and cannot look up or persist a single
   `Rating`), and consequently **no Ratings.Application command/use-case and no HTTP endpoint** to
   record a rating (`/me` proves the auth seam but no rating write exists yet). Because there is no
   per-`Rating` write port, the use-case as otherwise described could neither persist the fact nor
   decide revise-vs-new, and the recompute/aggregate path would have nothing new to recompute from —
   so the write port is a **prerequisite capability** of this scope, not an optional extra.
   *In-scope work (high-level capability, no code):*
   - a **new rating write port in the Ratings domain** — an `IRatingRepository`-style abstraction that
     can **look up an existing `Rating` by user + restaurant** and **save or update a `Rating` fact**
     — **plus its Infrastructure adapter** over the `ratings.Rating` fact table. This is what gives
     the use-case the revise-vs-new semantic (find-then-revise-or-create) and what writes the new fact
     that the existing recompute/aggregate path then rolls up; it sits alongside, and does not replace,
     the existing aggregate-rollup repository;
   - a **submit-rating application use-case** that, for the authenticated caller, uses that write port
     to look up any existing rating and persist one `Rating` fact (restaurant + the user's opaque
     reference + score + timestamp) — revising in place when one already exists — and publishes the
     existing `RatingGiven` integration event, so the existing recompute job + cumulative all-time
     aggregate produce the **smoothed** value the recommend/details paths already display (invariant
     #6 — our-own, cumulative all-time, Bayesian-smoothed; nothing about how ratings are read or
     ranked changes);
   - an **authenticated `POST` rating endpoint on the Public host** behind the existing user policy
     (a valid Keycloak user token; the `/me` seam already resolves the caller's `sub`/roles), which
     drives that use-case.
   - **Design points for the architect to state at a high level:** the authenticated caller's
     identity (the IdP `sub`) is mapped to the rating's opaque user reference (still no PII stored);
     and **one-rating-per-user-per-restaurant** semantics — a user's later score for the same venue
     should **revise** their existing rating (the Domain already supports `Revise`, and the
     aggregate distinguishes a new score from a revision) rather than double-count, so the all-time
     count reflects distinct raters. Validation (score range, required references) is already
     enforced by the Domain.
2. **Analytics read endpoint + rollup-reader extension (Admin host), aggregates-only.** Today
   `IAnalyticsRollupReader` lives in the Analytics.Application layer but is **internal/test-only**
   and exposes **only** a minimal rollup: impressions / card-opens / CTR **per restaurant per hour**
   (its SQL groups the append-only event store and projects counts only). There is **no HTTP
   endpoint**, and the richer §7.5 metrics are not surfaced even though the stored, anonymized event
   dimensions (category/dish ids, sort mode, filter keys, result position, coarse geohash, hour
   bucket) can support them. *In-scope work (high-level capability, no code/SQL):*
   - **extend the rollup-reader seam** with additional **aggregate** queries to back the §7.5
     dashboard metric families: visibility/traffic (impressions, card-opens, CTR, action clicks),
     demand by category/dish (which selections are searched in an area), price positioning (the
     venue's prices vs the area/category median the engine already computes), ratings distribution
     (from the ratings aggregate the recompute job materializes), and the conversion funnel
     (impression → card-open → action). Every query returns **grouped counts/ratios only**;
   - expose an **authenticated, admin/operator-scoped (admin policy) analytics read endpoint on the
     Admin host** that returns those aggregates.
   - **Privacy guarantee (invariant #11, stated explicitly):** by query shape the seam emits
     **aggregates only** — no actor hash, no per-event id, no precise coordinate, no per-user row
     ever crosses the boundary; venues/operators see counts/ratios, never personal data. The
     endpoint inherits the same admin-only policy as the rest of the Admin host. The current backend
     provides only the minimal rollup, so these richer aggregates are **new queries that are part of
     this scope**, deliberately kept aggregate-only.
3. **No restaurant-owner self-serve / claim-verify API (deliberately out of scope).** Management
   endpoints are admin-policy-only on the Admin host, and the new analytics endpoint follows the
   same gate. *Decision (stated):* the portal targets the **admin/operator persona** and wires the
   existing admin endpoints + the new analytics endpoint; owner self-serve and claim/verify remain a
   **future backend dependency**, with the feature structure and role-gating designed to absorb an
   owner persona and owner-scoped endpoints later without restructuring. Flagged as a conscious scope
   boundary, not added to this plan.

### Resolved decisions (open questions the user answered)

4. **Auth / IdP — RESOLVED: Keycloak (self-hosted), OIDC Authorization Code + PKCE.** The SPA
   integrates with Keycloak; the backend already validates such tokens (JWKS) and already maps a
   Keycloak-style `admin`/`user` role from the token's role claim onto its own role enum, with a
   swappable claims-to-role mapper. The admin/operator role for portal/analytics gating comes from
   Keycloak; the bearer is attached to the Public host (rating-submit) and Admin host (portal +
   analytics). *Remaining small note:* the concrete Keycloak **realm/client, the exact role-claim
   location** (realm-role vs client-role placement in the token, e.g. `realm_access.roles` vs a
   flattened `roles`/`role` claim), and the client id / redirect URIs / scopes are **environment
   config** to confirm; if Keycloak nests roles differently from the backend's default flat
   `role`/`roles` mapping, a custom claims-to-role mapper may be needed server-side (the seam already
   supports swapping it).
5. **CORS / origin — RESOLVED: single origin via a reverse proxy / API gateway.** A gateway sits in
   front of **both** backend hosts so the SPA talks to **one origin** and uses relative `/api/...`
   paths in every environment; CORS is handled by infra, not per-host backend config (neither host
   registers CORS today, and with this model neither needs to). The data layer still distinguishes
   the two logical hosts by route prefix behind the gateway. *Remaining small note:* the **exact
   gateway product** and its dev incarnation (Angular dev-server proxy vs a local gateway) are an
   infrastructure choice to confirm per environment.
6. **Map provider — RESOLVED: swappable map-provider interface, Google Embed credential from
   config.** The map source is abstracted behind a provider interface (swappable / portable to React
   Native); the Google Maps Embed credential is environment config, and with no credential present
   the provider degrades to a PlaceId / Maps deep-link. §5.7 semantics are preserved (live, nothing
   cached, no Google rating/coords stored; the `/map` payload supplies Place ID / deep-link /
   coordinates). *Remaining small note:* the Embed credential value and its quota posture are a
   per-environment config concern to confirm.
7. **Languages / locale — RESOLVED: Ukrainian-first, localization-ready.** Launch locale is `uk`
   (default and only populated locale); the full i18n plumbing (catalogs in a dedicated,
   view-independent i18n module, locale-aware number/distance/currency formatting, a wired language
   switch) is built so other
   European languages can be added later with no architectural change. Currency is formatted by the
   backend's per-item / per-basket `PriceCurrency` (₴ expected at launch), not hardcoded. No
   remaining open question on this item.

### Remaining open question

8. **Ad-slot shape in recommend results is undefined.** Invariant #10 requires labeled,
   visually-separated ad slots, but the current `recommend` response has no ad-placement field. The
   UI is structured to render labeled slots when present; the **exact response contract for ad
   slots** (shape, position semantics, label/marker fields) remains an **open question** for when
   monetization surfaces paid placements in search. Out of scope for this plan beyond the
   presentation contract the UI is structured to honor.
