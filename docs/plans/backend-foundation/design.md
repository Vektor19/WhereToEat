# Design — backend-foundation

> High-level design for the **backend foundation** of WhereToEat / ДеПоїсти.
> Greenfield: no backend code exists yet. This document is grounded in `CLAUDE.md`
> (the product source of truth, with its **залізні інваріанти**) and the verbatim
> backend request. It is **design only** — exact file paths, project names, imports,
> and code belong in `implementation.md`.

## Goals

1. **Establish the .NET backend foundation** — a solution skeleton, layering, and
   conventions that let a team grow to 10–20+ projects without the structure rotting.
2. **Full domain core** — a rich, persistence-agnostic domain model describing the ER
   model required by the product: the two-level taxonomy (Category → Dish), Restaurants,
   Menu items, Addresses/Coordinates (own geo), Users, our own cumulative Ratings, Photos
   (generic-by-default), Verified/subscription status, Ads/Promotions, and anonymized
   Analytics events. Relationships and invariants encoded in the model, not just the DB.
3. **Onion/Clean layering with explicit module boundaries** — dependencies point inward,
   the Domain core stays framework- and persistence-free, and the solution partitions into
   layer × module project families that scale to 10–20+ projects without structural rot.
4. **Pluggable parser subsystem** — a strategy abstraction with at least a working
   skeleton, supporting a **Selenium-based** primary strategy and a **library-based**
   (AngleSharp / HtmlAgilityPack) strategy, with a seam for a future **Puppeteer-based**
   locator strategy. Parser respects robots.txt + rate-limit, first-party sites only, and
   is **subordinate to the admin** (do-not-parse / do-not-update flags).
5. **Pluggable recommendation engine** — the 5-step pipeline from CLAUDE.md §6 with
   `match` predicates (OR / AND / future K-of-N), per-restaurant aggregation, ranking where
   **explicit sort dominates** and coverage is only a tie-breaker, composite-score modes,
   and composable filters — all as independent, DI-registered modules.
6. **Data access prioritizing flexibility + performance "like ADO.NET"** — Dapper over a thin
   data layer with hand-tuned SQL on the hot paths, and EF Core scoped strictly to admin CRUD,
   with the versioned SQL migration scripts (not EF) owning the schema.
7. **Configurable horizontal scaling** — stateless services on containers + an orchestrator,
   with externalized Redis cache and a MassTransit/RabbitMQ bus, so scaling is a
   config/deployment concern (replica count / autoscaling), not a rewrite.
8. **Test architecture as a first-class concern** — **e2e tests per restaurant** for the
   Selenium parser and **integration tests** for the library parser, designed so each new
   restaurant parser adds its own e2e test with minimal ceremony.
9. **Honor every CLAUDE.md invariant** that touches the backend (deterministic search,
   admin > parser, our ratings only, own geo, generic photos, labeled ads, privacy).

## Non-Goals

- The **mobile app and web frontend** themselves (the backend exposes the API they consume).
- A **full admin UI** — we define the admin API surface and protection semantics, not the UI.
- **Real payment / billing integration** for Verified/ads — modeled as future-facing
  domain + seams only; no payment provider.
- **Production-grade locator scraping** (Puppeteer restaurant discovery) — only an
  abstraction seam; not implemented now.
- **Complete coverage of every monetization product** (B2B analytics dashboards, market
  insights) — the data model and event collection must *accommodate* them, but the
  dashboards are out of scope.
- **NLP / heavy-AI search** — explicitly excluded by invariant #1; search is deterministic.
- **Caching or storing Google ratings / raw Google lat-lng** — forbidden by invariants #6/#7.

## Overview

### Architecture style — modular monolith with service seams (evolving to services)

We adopt a **modular monolith built on Clean/Onion layering** (committed, Q1), partitioned into
**vertical feature modules** that each own their domain, application, and infrastructure
slices behind a clear contract. This is the pragmatic middle path the request asks for:

- It satisfies "non-monolithic, maintainable, scalable" and "10–20+ projects easy to
  maintain" via **strong module boundaries** (each module is its own set of projects with
  enforced dependency direction), **without** paying the day-one operational tax of true
  microservices (separate deploys, network hops, distributed transactions, schema-per-service).
- The **service seams are explicit from day one**: modules talk to each other only through
  published contracts and an in-process message bus (later swappable for a network bus), so
  a hot module (e.g., the recommendation engine, the parser, or analytics ingestion) can be
  **extracted into its own deployable service** when load justifies it — a deployment change,
  not a redesign.
- **Onion/Clean per module**: dependencies point inward (Infrastructure → Application →
  Domain). The Domain core has **no framework or persistence dependencies**, which is what
  makes the data-access choice swappable and the domain testable in isolation.

Trade-off acknowledged: a modular monolith requires **discipline** to keep boundaries from
eroding (a code-level dependency can sneak across modules in a way it cannot across a network).
We mitigate with architecture/dependency tests in the test suite (fitness functions) that fail
the build if a module reaches into another module's internals or if the Domain references
infrastructure. We deliberately reject **true microservices now** because the team has no
traffic data yet, and premature service splitting would multiply cost (ops, latency, eventual
consistency) before it buys anything. **This is a committed decision** (Q1): modular monolith
with service seams — modules share a process today but have hard, fitness-test-enforced
boundaries and a **swappable in-process → network bus**, so any hot module (parser,
recommendation) can be extracted into an independently-deployed service later as a **deploy
change, not a rewrite**. NOT day-one microservices.

### Logical layering (Onion / Clean), repeated per module

- **Domain** — entities, value objects, domain rules, taxonomy invariants, the
  recommendation/match/sort **strategy abstractions** (interfaces + pure scoring math). No
  I/O, no framework. This is the "full domain core models layer" the request asks for.
- **Application** — use-cases / orchestration (search, recommend, rate, run-parse,
  admin-edit), defined as request→handler flows; depends only on Domain + abstractions
  (ports) for persistence, geo, messaging, clock, etc.
- **Infrastructure** — concrete adapters: the SQL data layer, the OSM/Nominatim geocoder,
  the Selenium/library parsers, the cache and message-bus implementations, the Google Maps
  Embed link/Place-ID helpers (link-only — no Google rating/coords persisted).
- **Host / API** — the web entry points: a **public-API host** (catalog/search/recommend/
  ratings + analytics ingest) and a **separate admin-API host** (committed, Q4), each with its
  own composition root wiring strategies via DI and validating OIDC tokens from the external IdP.
- **Background worker host** — the weekly parse scheduler, normalization, geocoding, and
  analytics aggregation jobs. Separately deployable so heavy parsing never competes with the
  request-serving API for resources (independent horizontal scaling).

### How the solution partitions into many projects (project families, conceptual)

The 10–20+ projects come from **(layer × module) plus shared kernels and test projects**,
not from one giant flat list. Conceptually:

- **Shared kernel family** — cross-cutting primitives (result/error types, value objects
  like Money and GeoPoint, the Haversine utility, base specifications) and the
  contracts/abstractions shared across modules. Kept small and stable.
- **Catalog & Search module** — taxonomy, restaurants, menus, deterministic search.
- **Recommendation module** — the pluggable engine (match / aggregate / rank / filter / sort).
- **Ratings module** — our own cumulative, smoothed ratings.
- **Geo module** — coordinates, geocoding port, distance.
- **Parsing module** — strategy abstraction + Selenium/library strategies + robots/rate-limit.
- **Admin module** — manual edits and the protection flags (admin > parser).
- **Analytics module** — anonymized event ingestion and aggregation.
- **Monetization module (future-facing)** — Verified status, ads/promotions (labeled), with
  payment kept behind a seam.
- **Hosts** — **separate public-API host and admin-API host** (committed, Q4), plus the worker
  host. Two distinct host processes for security isolation and independent scaling; this is a
  natural service seam consistent with the modular-monolith decision.
- **Test families** — unit, integration (incl. library-parser + DB), and **per-restaurant
  Selenium e2e**, plus architecture-fitness tests.

Each module typically contributes a Domain, Application, and Infrastructure project, so the
count scales naturally with the number of modules while the *shape* stays uniform and learnable.

## Key Components & Flows

### Domain core & ER model (conceptual entities + relationships)

Two-level taxonomy is an **invariant** (#2): Category is a *group*, Dish is a *position within
a category*. Menu items are dish-at-restaurant facts. Conceptual entities and relationships:

- **Category** — group of dishes (Перші страви, Фаст-фуд…). `1 — many` Dishes.
- **Dish** — a concrete position (Борщ, Піца Маргарита). Belongs to exactly one Category
  (`many Dishes — 1 Category`). A canonical/normalized name lives here; raw parsed names map
  to it during normalization.
- **Restaurant** — a venue. Has one **Address** and one **Coordinates** (own geo). Has many
  **MenuItems**, many **Ratings**, optional **VerifiedStatus**, many **Photos** (via dishes),
  many **ContactLinks** (site/socials — always shown, never monetized).
- **MenuItem** — *dish at a restaurant*: links `Restaurant — Dish`, carries **price**,
  **weight/quantity**, source (parsed vs admin), and a **`do-not-parse` flag** (admin > parser).
  This is the join entity that makes both category-search and dish-search work.
- **Address** — postal address; parsed then admin-editable; triggers re-geocode on change.
- **Coordinates** — our lat/lng from **OSM/Nominatim only** (ODbL-storable) + optional Google
  **Place ID** (storable) + a Maps **deep-link**. **Never** raw Google lat/lng (invariant #7).
  Stored in MS SQL as the **`geography` spatial type with a spatial index** (committed, Q3) so
  the DB can do index-assisted **radius pre-filtering** (narrow candidates to a bounding
  circle). Exact distance for display and ranking is still computed locally via **Haversine**
  (invariant #7) — the spatial index narrows, Haversine decides. No stored Google geo, no paid API.
- **ContactLinks** — the venue's **website / social-media links** (`many ContactLinks — 1
  Restaurant`). Parsed or hand-entered, and shown **always and for free** in restaurant
  details, even for non-Verified venues — links are **never** monetized (invariant #10,
  CLAUDE.md §5.8). Listed as its own entity so the model matches the prose under Restaurant
  above.
- **User** — registered user (for ratings); PII minimized and never exposed to venues.
- **Rating** — a single user's score for a restaurant. Aggregated into a per-restaurant
  **cumulative, all-time** average **plus review count**, **smoothed toward neutral** when
  counts are low (invariant #6). The committed smoothing method is a **Bayesian
  (additive-prior) average**: `smoothed = (C·m + Σratings) / (C + n)`, where `m` is the
  **global mean rating** across all venues (the neutral prior the score is pulled toward), `n`
  is the venue's review count, `Σratings` is the sum of its ratings, and `C` is a **smoothing
  constant** — the prior weight, i.e. how many "phantom votes" of the global mean a venue
  carries before its own ratings dominate. `C` (and the source of `m`) are **configurable**, so
  a single all-time five-star venue stays near neutral until enough real reviews accumulate,
  while a venue with hundreds of ratings is effectively its own raw average. This is the one
  authoritative formula all implementers converge on (`f_quality` in the recommendation
  pipeline consumes this same smoothed value). Google ratings are **not** modeled/stored —
  shown live only via Embed at the UI layer.
- **Photo** — image for a dish/category. **Generic-category photo is our own content and is
  the default on every dish** (invariant #8). Real photos exist only for permission-granted
  (Verified) venues; a permission flag gates them.
- **VerifiedStatus / Subscription** — venue's official status + tier (Basic/Pro), future-facing.
- **Promotion / AdPlacement** — paid, **always labeled** placement (invariant #10). Organic
  ranking is **not** for sale — ads are a separate, marked slot, never a rank modifier.
- **RestaurantProtection** — the **"edited manually / do-not-update"** flag set (admin > parser,
  invariant #3) guarding a whole restaurant from parser overwrite; complements the per-MenuItem
  do-not-parse flag.
- **AnalyticsEvent** — anonymized event (impression / view / card_open / action / search /
  filter / rating_given / geo / session). Stored for aggregation; venues see **aggregates only**
  (privacy invariant #11). Append-heavy, write-optimized, conceptually separable from the
  transactional catalog store. **Anonymization is applied at ingest, before persistence**, so
  raw PII is **never written to the store**, and the persisted shape is fixed by three rules:
  - **Retained as-is** — non-identifying analytical dimensions: dish/category ids, sort mode
    and filter selections, the result **position** an impression occupied, the **coarse area**
    (a low-precision **geohash** at a reduced number of characters — neighbourhood-grade, not
    point-grade), and the **timestamp truncated to the hour**.
  - **Hashed / pseudonymized** — any user or session identifier is replaced by a **rotating
    salted hash** (a salt rotated on a schedule), so events can be correlated within a window
    for funnel/retention math without being reversible to a person or linkable across windows.
  - **Dropped** — **precise lat/lng is never stored** (only the coarse geohash survives), and
    no raw PII (names, contact details, exact identifiers) is persisted at all.

  Because stripping happens at ingest and venue-facing outputs are always aggregates, the
  privacy invariant holds end-to-end.

### Data-access strategy

Committed (Q2): **Dapper over a thin repository/data layer with hand-tuned SQL on the hot
paths** (catalog / search / recommendation), on **MS SQL Server**. This honors "most flexible
with performance like ADO.NET": Dapper is a thin, fast mapper over ADO.NET that keeps
hand-written, tuned SQL (critical for the search/recommendation hot paths and for SQL-side
spatial/aggregation work, including the `geography` radius pre-filter) while removing the
boilerplate of raw `SqlConnection`/`SqlDataReader`. Repositories expose Domain-shaped methods;
the Application layer never sees SQL.

**EF Core is used ONLY for the low-traffic admin CRUD surface** — manual data curation,
protection flags, Verified/photos — where developer velocity (change-tracking, rapid
scaffolding) matters more than throughput. EF is kept behind the admin module's own boundary so
it never touches the read/recommend hot path.

**Schema-of-record is the versioned SQL migration scripts, not EF.** Schema lives in
**versioned SQL migration scripts** run by a lightweight migration runner
(e.g., DbUp/FluentMigrator-style). To avoid a second source of truth, **EF does NOT own
migrations**: its model is **mapped to the existing, externally-managed schema** (database-first
mapping), and EF's own migration/schema-generation pipeline is **disabled** (no
`Migrations`/`EnsureCreated`; the DbContext is configured to never alter the schema). This
resolves the EF-coexists-with-non-EF-migrations tension: SQL scripts create and evolve the
tables; EF merely reads and writes admin rows over the schema those scripts produced.

### Parsing pipeline (collect → normalize → geocode → persist, admin-protected)

1. **Collect** — for each restaurant source (a first-party site), a parser strategy fetches
   menu facts. Strategy is selected per-source: **Selenium** (primary, handles JS-rendered
   sites) or **library-based** (AngleSharp/HtmlAgilityPack, for static HTML). A future
   **Puppeteer** locator strategy plugs into the same abstraction. **Compliance gates run
   first:** robots.txt allowance + per-host rate-limit; **first-party only** (no aggregators);
   no tech-protection bypass (invariant #9).
2. **Normalize** — map raw dish names to the canonical two-level taxonomy (Category → Dish),
   unify units/prices to one format.
3. **Geocode** — resolve/refresh address → coordinates via **OSM/Nominatim**; store our coords
   as the SQL `geography` type (+ Place ID if available). Re-geocode when an address changes.
4. **Persist with admin protection** — **before writing**, check protection flags: skip any
   restaurant flagged **do-not-update** and any MenuItem flagged **do-not-parse**. The parser
   is a "rough fill"; the admin is the source of truth and is never overwritten (invariant #3).

The whole pipeline runs as a **weekly background job** (CLAUDE.md §5.1) in the worker host, and
parser strategies are **registered as plug-ins** so adding a restaurant = adding a strategy +
its e2e test, with no engine changes.

### Recommendation pipeline (the 5 steps from CLAUDE.md §6)

The request is `{ items, match, sort, filters, userGeo }`. The engine is a **strategy
conveyor**, not one formula:

1. **Candidate selection (match predicate)** — `OR` (≥1 selected item present) / `AND` (all
   present, the "combo" search) / future "K-of-N". The match strategy is a pluggable predicate.
2. **Per-restaurant aggregation** — basket price (cheapest matched for OR, sum for AND), quality
   (our smoothed all-time rating + count), distance (Haversine), and **coverage** (how many
   selected items are present).
3. **Ranking — two clear cases.** **(A) Explicit sort (price / distance / rating):** that field
   is the **primary key**; everything else (especially **coverage**) is **only a tie-breaker**
   (invariant #5) — the cheapest venue wins even if it has 1-of-3, and 3-of-3 only breaks a tie
   between equal prices. **(B) Composite mode ("price-quality" / "best"):** compute a composite
   **score** from normalized `f_price ⊗ f_quality ⊗ f_distance ⊗ f_coverage` and sort by it.
   Each `f_*` is a **normalized (0…1)** scoring function with **per-mode weights**; distance is
   on by default but user-disable-able.
   - **`f_price` normalizer + cold-start.** `f_price` measures how cheap a venue's basket is
     relative to the **market median of the same dishes** (CLAUDE.md §6). The committed approach:
     normalize against the **per-area / per-category median** of those dishes; when the
     per-area sample size is below a **configurable threshold N** (too few venues in the area to
     trust the local median), **fall back to the category-wide (city) median**. Medians are
     **not computed per request** — they are **precomputed and refreshed by a nightly background
     job** from `MenuItem` prices (and cached, per the Redis note below), so the hot
     recommendation path only reads a ready value. This resolves both the "compared against
     what?" question and the small-area cold-start.
4. **Filters** — composable, applied over the ranked result (price, rating, future: open-now,
   vegan, delivery). New filters are independent modules.
5. **Sort & deliver** — final ordered, filtered list. Match-modes, sort-modes, and filters are
   **independent extensible modules** (invariant #4), each registered via DI and discovered by
   key from the request, so new modes/filters add **without** touching existing ones.

Search itself stays **deterministic** (invariant #1): users pick from category/dish lists; no
NLP — selection drives a fast, predictable query.

### API surface (conceptual)

- **Catalog** — browse categories, dishes within a category, restaurant details (details
  **always** include site/social links, free, even when not Verified — invariant §5.8).
- **Search** — deterministic list/lookup of categories and dishes to build the selection set.
- **Recommend** — execute the 5-step pipeline for a selection set + mode + filters + geo.
- **Ratings** — submit a rating (authenticated users); read the aggregate (cumulative, smoothed).
- **Map** — return the Google Maps **Embed** payload / Place ID / deep-link for "Глянути на
  карті" (live only, nothing cached) (§5.7).
- **Admin** — manual menu/price/category edits, address edits (+ re-geocode), the **protection
  flags**, and real-photo management for Verified venues. Served by the **separate admin-API
  host**, authorized by **admin-only OIDC roles/scopes** from the external IdP; admin > parser.
- **Analytics ingest** — accept anonymized events (impression/view/card_open/action/search).
  Append-optimized; aggregation happens in background jobs; venues only ever read aggregates.

### Background jobs

- **Weekly parse** (collect→normalize→geocode→persist, admin-protected).
- **Geocoding** refresh on address change.
- **Nightly price-median refresh** — recomputes the per-area / per-category (and city-wide
  fallback) medians that `f_price` reads, from current `MenuItem` prices.
- **Analytics aggregation / rollups** feeding future B2B dashboards (aggregates only).
- **Rating recomputation** if aggregates are materialized rather than computed on read.

Jobs live in the worker host and scale independently of the API. Because the worker can run as
**multiple replicas** (horizontal scaling), scheduled jobs use a **distributed-safe scheduler**
so a given run fires on exactly one replica — the committed/candidate choice is **Quartz.NET
with a clustered (DB-backed) job store** (Hangfire is an equivalent candidate). This prevents
two workers from double-running the weekly parse or the nightly median refresh.

### Cross-cutting concerns

- **Config-driven horizontal scaling** — request-serving services are **stateless**; anything
  that would pin a request to an instance (cache, sessions/tokens, locks, the message bus) is
  **externalized**. The committed stack (Q5) reinforces this: services ship as **Docker images
  on an orchestrator (Kubernetes or equivalent)**, so the **number of replicas** for each host
  (public API, admin API, worker) is **config-driven horizontal scaling** (replica count /
  autoscaling). Whether a hot module runs in-process or as a split-out service is likewise
  configuration. Background-job scheduling uses a **distributed-safe scheduler — Quartz.NET with
  a clustered (DB-backed) store** (Hangfire an equivalent candidate) — so multiple worker
  instances don't double-run the weekly parse or the nightly median refresh (only one replica
  runs any given scheduled job).
- **Caching** — **Redis** (committed, Q5) behind a clean cache abstraction, used for hot
  catalog/recommendation read paths (taxonomy lists, category-median prices used by `f_price`,
  rating aggregates), rate-limit counters, and to keep services stateless. The abstraction lets
  the backend swap providers without touching call sites. Never cache Google ratings/coords.
- **Messaging** — **MassTransit as a broker-agnostic abstraction with RabbitMQ as the default
  broker** (committed, Q5). **In-process transport now, network broker (RabbitMQ) when modules
  are split into services** — one abstraction, swapped by configuration. Carries
  domain/integration events (e.g., "menu updated", "rating given", "analytics event") — this is
  the seam that lets modules split into services without a rewrite.
- **Logging / observability** — structured logging, correlation IDs across API↔worker, metrics
  and tracing, so a future service split is observable from day one.
- **Auth — external identity provider via OpenID Connect** (committed, Q5; e.g.,
  Keycloak / Auth0 / Entra). We do **not** roll our own token issuance. The backend hosts are
  **OIDC resource servers**: they **validate OIDC tokens** issued by the external IdP and map
  claims to **our user/role model** behind an identity abstraction, so the chosen IdP is
  swappable. The **separate admin API host** validates the same tokens but authorizes only
  admin roles/scopes — admin authentication and authorization run through the same external IdP
  and identity abstraction, just with admin-only role checks and stricter network isolation
  (no rolled-our-own admin auth). Public, authenticated-user, and admin scopes stay clearly
  separated; privacy-by-design keeps venue-facing data aggregate-only.

## Key Decisions & Trade-offs

- **Data access: Dapper on the hot paths + EF Core for admin CRUD only** (committed, Q2).
  *Why:* matches the explicit "flexible + performant like ADO.NET" requirement; keeps tuned SQL
  for catalog/search/recommendation; less boilerplate than raw ADO.NET; EF buys developer
  velocity on the low-traffic admin surface. *Trade-off:* hand-written SQL is more upfront work
  than an ORM's scaffolding — accepted for the read/recommend core. **Two data-access stacks in
  one system** is the cost; bounded by keeping EF strictly inside the admin module and giving it
  **no ownership of the schema**: the schema-of-record is the versioned SQL migration scripts,
  EF is mapped database-first to that schema with its migration pipeline disabled.
- **Architecture: modular monolith on Onion/Clean, with service seams** (committed, Q1). *Why:*
  delivers the "non-monolithic, 10–20+ projects, scalable" goal without premature-microservices
  cost; seams let hot modules be extracted later as a deploy change. *Trade-off:* needs
  discipline to keep boundaries — mitigated with **architecture-fitness tests that fail the
  build**, written with a concrete tool: **NetArchTest** (or **ArchUnitNET** as an equivalent
  candidate). These mechanically enforce the dependency direction (Domain references no
  infrastructure; EF references stay inside the admin module; modules reach each other only
  through published contracts) so a boundary violation breaks CI rather than relying on review.
- **In-process MassTransit coupling — fitness tests must cover consumers, not just the API
  layer.** In the modular monolith **all message consumers share one process and one IoC
  container**, so MassTransit decouples the *call*, not the *reference* — nothing physically
  stops a consumer in one module from taking a hard code reference into another module's
  internals, a leak a network bus would have made impossible. The architecture-fitness tests
  must therefore explicitly assert **consumer-to-contract and cross-module references** (each
  consumer depends only on the shared contracts, never on another module's domain/application
  internals), not merely the API-layer references. Without that rule a cross-boundary coupling
  could slip in that no test catches and that would block a later service extraction.
- **Separate public-API and admin-API hosts** (committed, Q4). *Why:* security isolation
  (admin curation surface segregated from the public internet-facing API) and independent
  scaling; a natural service seam that fits the modular monolith. *Trade-off:* two host
  processes to operate instead of one — accepted, and cheap under the container/orchestrator
  deployment since each is just another set of replicas.
- **Horizontal scaling is configuration, not code** (reinforced by the committed Q5 stack).
  Stateless services + externalized **Redis** cache, **MassTransit** bus, and a distributed
  scheduler mean replica count and backends are config; the same code runs as a monolith or as
  split services on **Docker + orchestrator (Kubernetes or equivalent)**. *Trade-off:* requires
  the externalized infrastructure to exist even at small scale — kept swappable behind clean
  abstractions (cache, bus, identity) so local dev can use lighter substitutes.
- **External OIDC identity provider, not rolled-our-own** (committed, Q5). *Why:* token
  issuance, password storage, MFA, and account recovery are solved problems with real security
  risk if homegrown; an external IdP (Keycloak / Auth0 / Entra) lets the backend be a pure OIDC
  resource server. *Trade-off:* an external dependency and IdP config to operate — bounded by an
  identity abstraction that maps OIDC claims to our user/role model so the IdP is swappable.
- **Pluggability via strategy pattern + DI registration.** Match-modes, sort/score modes,
  filters, and parser strategies are interfaces resolved **by key** from the request/config;
  new ones self-register. *Why:* invariants #1/#4 and the "independent extensible modules"
  requirement. *Trade-off:* an indirection layer vs. hard-coded logic — justified by the
  explicit extensibility mandate.
- **MS SQL specifics.** Store our coordinates as the SQL Server **`geography` spatial type with
  a spatial index** (committed, Q3) for index-assisted radius **pre-filtering**, then compute
  exact distance via **Haversine** locally for display/ranking (invariant #7). *Trade-off:* the
  spatial type/index is heavier than plain decimals, but pays off once the catalog is large and
  radius filters dominate; raw Google lat/lng is never stored, Place ID is allowed. **JSON
  columns** for flexible, low-churn payloads (e.g., raw parsed snapshots, event detail bags)
  where a normalized table adds no query value. **Migrations via versioned SQL scripts + a
  runner**, not EF migrations (EF mapped database-first, migrations disabled).
- **Selenium e2e test approach.** Each restaurant parser gets **its own e2e test** that drives
  the real Selenium strategy against a **pinned, version-controlled HTML fixture / recorded
  page** (not the live site in CI — avoids flakiness, rate-limit, and ToS issues) and asserts
  the normalized menu output. A shared e2e harness makes "new restaurant = new test class +
  fixture" the only work. **Library parsers** get **integration tests** over fixtures asserting
  the same normalized contract. A small, clearly-marked **live smoke** suite can run
  out-of-band (manually / nightly) to catch real site drift, respecting robots/rate-limit.

## Risks & Open Questions

### Resolved foundational decisions (all five open questions answered — folded in above)

- **Q1 — Architecture:** **Modular monolith with service seams** (Onion/Clean), fitness-test
  enforced boundaries, swappable in-process → network bus so hot modules split later as a deploy
  change. NOT day-one microservices.
- **Q2 — Data access:** **Dapper + hand-tuned SQL on hot paths**, **EF Core only for admin
  CRUD**; schema-of-record is the **versioned SQL migration scripts** (EF mapped database-first,
  EF migrations disabled).
- **Q3 — Geo storage:** SQL Server **`geography` type + spatial index** for radius
  pre-filtering; exact distance via **Haversine** locally. No raw Google lat/lng; Place ID OK.
- **Q4 — API hosts:** **Separate public-API and admin-API hosts** for security isolation and
  independent scaling.
- **Q5 — Stack & identity:** **Docker + orchestrator (Kubernetes or equivalent)** with
  config-driven replica scaling; **Redis** distributed cache (behind an abstraction);
  **MassTransit** broker-agnostic bus with **RabbitMQ** default broker (in-process now); auth via
  an **external OIDC identity provider** (Keycloak / Auth0 / Entra) — no rolled-our-own token
  issuance.

### Decided since the A− review (folded into the sections above)

These were previously soft spots; each now has a committed approach in the design body:

- **Rating smoothing method — decided.** Bayesian additive-prior average
  `smoothed = (C·m + Σratings) / (C + n)` with global mean `m` and configurable prior weight
  `C` (see the Rating entity). One authoritative formula for all implementers.
- **`f_price` cold-start & refresh — decided.** Per-area / per-category median with a
  city-wide fallback below a configurable area threshold `N`; medians precomputed by a
  **nightly background job** from `MenuItem` prices (see the recommendation pipeline and
  Background jobs). The earlier "non-trivial sub-task" is now specified, not open.
- **Analytics anonymization — decided.** Retain / hash / drop rules applied **at ingest** so
  raw PII is never stored, coarse geohash + hour-truncated time + rotating salted id-hash (see
  the AnalyticsEvent entity). Privacy invariant holds at the data level, not just at output.
- **Fitness-test tooling — decided.** **NetArchTest** (ArchUnitNET as candidate) for
  build-failing boundary tests, and those tests must cover **consumer-to-contract / cross-module
  references**, not only the API layer (see Key Decisions & Trade-offs). The "discipline" risk of
  the modular monolith is now mechanically enforced.
- **Distributed scheduler — decided.** **Quartz.NET with a clustered (DB-backed) store**
  (Hangfire candidate) so only one worker replica runs each scheduled job (see Background jobs).

### Residual risks (no blockers)

- **Two data-access stacks in one system.** Dapper + EF must not bleed into each other. Bounded
  by keeping EF strictly inside the admin module and denying it schema ownership; the
  **NetArchTest** fitness suite forbids EF references outside the admin module. → risk.
- **Live-site parser drift vs. CI stability** — fixture-based e2e is stable but can go stale
  relative to real sites; the out-of-band live-smoke cadence and ownership need agreeing. → risk.
- **Analytics store coupling** — append-heavy analytics may eventually want a separate
  store/path from the transactional catalog; the model keeps them separable, but the split
  timing is a future decision. → risk (no blocker now).
- **External IdP operational dependency** — the OIDC provider is now on the critical path for
  all authenticated requests; provisioning, token-validation caching (JWKS), and a local-dev
  substitute need to be handled in the implementation plan. → risk (no blocker now).

### CLAUDE.md invariant check against the committed stack

No CLAUDE.md invariant is violated by the committed stack. Specifically: deterministic search
(no NLP) is untouched by Dapper/EF/Redis; **`geography` + spatial index is only a pre-filter —
exact distance is still local Haversine** (invariant #7 preserved) and **no raw Google lat/lng
is stored** (Place ID only); **admin > parser** is preserved (EF only powers admin CRUD, the
protection flags still gate the parser, and the separate admin host strengthens this boundary);
own ratings / no Google cache, generic-photo default, labeled-and-unsold organic ranking, and
aggregate-only privacy are all unchanged. The external IdP touches only authentication, not the
privacy invariant (venues still see aggregates only). If any later requirement pushes against
these, it must be flagged rather than designed around.

```yaml
step_id: "design"
status: completed
summary: Backend-foundation design revised per the A− architect-reviewer feedback — additive clarifications only, no structural change. Committed a Bayesian additive-prior rating smoother, the f_price per-area/category median with city-wide cold-start fallback refreshed by a nightly job, the analytics retain/hash/drop rules applied at ingest, NetArchTest fitness tooling (covering consumer-to-contract references, not just the API layer), Quartz.NET clustered scheduler, and ContactLinks as an explicit ER entity. No open questions remain; no invariant violations.
issues: []
notes: |
  Addressed all 7 review items, folded into existing sections and kept high-level (no file paths/code):
  (1) Rating smoothing: Bayesian additive-prior average smoothed=(C·m+Σ)/(C+n), C and m configurable — Rating entity.
  (2) f_price: per-area/per-category median, fall back to city-wide median below configurable threshold N; medians precomputed by a nightly job from MenuItem prices — recommendation pipeline + Background jobs.
  (3) Analytics anonymization: retain (dish/category ids, sort/filter, position, coarse geohash, hour-truncated time) / hash (rotating salted id) / drop (precise lat-lng, raw PII); stripped at ingest before persistence — AnalyticsEvent entity.
  (4) Fitness tooling: NetArchTest (ArchUnitNET candidate), build-failing — Key Decisions.
  (5) Distributed scheduler: Quartz.NET clustered DB-backed store (Hangfire candidate) — Background jobs + scaling note.
  (6) ContactLinks added as its own ER entity (was prose-only under Restaurant).
  (7) In-process MassTransit coupling: fitness tests must assert consumer-to-contract / cross-module references, not just API-layer — Key Decisions.
  Risks & Open Questions updated: items 1–5 moved from "residual/open" to a new "Decided since the A− review" subsection; the f_price market-median residual risk removed (now specified). Greenfield repo — no .NET code yet. Ready for re-review, then implementation.md.
```
