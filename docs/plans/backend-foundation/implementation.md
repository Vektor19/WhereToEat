# Implementation — backend-foundation

> Concrete, buildable steps for the WhereToEat / ДеПоїсти backend, derived from the **approved**
> `docs/plans/backend-foundation/design.md`. Greenfield .NET solution — every project and file
> below is new. This plan names real projects, namespaces, key files, NuGet packages, and SQL
> artifacts (the design's "no file paths" rule is intentionally lifted here).
>
> Every step honors the CLAUDE.md залізні інваріанти: deterministic search (no NLP), two-level
> taxonomy Категорія→Страва, admin > parser, pluggable recommendation engine, explicit sort
> dominates, our own ratings/geo, generic photos, privacy (aggregates only), labeled ads.

## Conventions for this plan

- **Target framework:** **.NET 8.0** (LTS) for all projects. `LangVersion` latest, `Nullable`
  enabled, `ImplicitUsings` enabled, `TreatWarningsAsErrors` true — set centrally in
  `Directory.Build.props`.
- **Solution root:** `D:\Projects\VS_Projects\DePoisty\src` holds the solution; tests live under
  `D:\Projects\VS_Projects\DePoisty\tests`; SQL migration scripts under
  `D:\Projects\VS_Projects\DePoisty\db`; container assets under
  `D:\Projects\VS_Projects\DePoisty\deploy`.
- **Solution file:** `src\WhereToEat.sln`.
- **Root namespace prefix:** `WhereToEat`. Module projects follow
  `WhereToEat.<Module>.<Layer>` (e.g., `WhereToEat.Catalog.Domain`). Shared families use
  `WhereToEat.SharedKernel`, `WhereToEat.Contracts`, `WhereToEat.BuildingBlocks.*`.
- **Search sub-namespace:** deterministic search is a **sub-module of Catalog**, named
  `WhereToEat.Catalog.Search.<Layer>` (e.g., `WhereToEat.Catalog.Search.Application`). For all
  fitness/boundary purposes it counts as part of the **Catalog module** (module prefix
  `WhereToEat.Catalog`), so the convention-based assembly grouping in Step 2 treats
  `WhereToEat.Catalog.*` (including `WhereToEat.Catalog.Search.*`) as one module — the rules do
  not flag intra-`Catalog` references between `Catalog.*` and `Catalog.Search.*`.
- **Module-grouping rule for fitness tests:** an assembly's "module" is its **first namespace
  segment after `WhereToEat`** (e.g., `Catalog`, `Recommendation`, `Ratings`), with the single
  exception that `WhereToEat.Catalog.Search.*` belongs to the `Catalog` module. Shared families
  (`SharedKernel`, `Contracts`, `BuildingBlocks.*`) and `Host` assemblies are not modules.
- **Layer dependency rule (enforced by NetArchTest in Step 2):** `Domain` → nothing in our
  solution except `SharedKernel`; `Application` → `Domain` + `SharedKernel` + `Contracts`;
  `Infrastructure` → `Application` + `Domain` + `SharedKernel` + `Contracts` +
  `BuildingBlocks.*`; `Host` → its module `Infrastructure` projects + `Contracts`. **No module's
  non-Contracts assembly may be referenced by another module** (where "module" is defined by the
  module-grouping rule above, so `Catalog.Search.*` may freely reference `Catalog.*`). EF Core
  packages may be referenced **only** by `WhereToEat.Admin.Infrastructure`.
- **Test framework:** xUnit + FluentAssertions + NSubstitute. DB/integration tests use
  Testcontainers for .NET (SQL Server container). Architecture tests use NetArchTest.Rules.
- **Each step is independently buildable** (`dotnet build src\WhereToEat.sln` succeeds) and its
  tests pass (`dotnet test`) before the step is considered done.

---

## Implementation Steps

### Step 1: Solution skeleton, central build props, SharedKernel, and Contracts

- **status:** [x] done
- **description:** Create the solution and the two stable shared families that everything else
  depends on. The **SharedKernel** holds cross-cutting primitives with no I/O: a `Result`/
  `Result<T>` type and an `Error` record (code + message + type), the `Money` value object
  (amount + ISO currency, with equality and arithmetic guards), the `GeoPoint` value object
  (latitude/longitude with range validation), and a pure static `Haversine` distance utility
  (returns kilometres; this is invariant #7's local distance math, kept framework-free). The
  **Contracts** project holds the cross-module integration-event and shared DTO abstractions
  (e.g., `MenuUpdated`, `RatingGiven`, `AnalyticsEventRaised` event contracts and the public
  recommendation request/response shapes) — the only types modules are allowed to share. Central
  `Directory.Build.props` pins TFM, nullable, implicit usings, warnings-as-errors, and common
  analyzer settings so individual `.csproj` files stay minimal. Add a `Directory.Packages.props`
  with **central package management** (`ManagePackageVersionsCentrally=true`) so all NuGet
  versions are pinned in one place.
- **files to modify:**
  - `Directory.Build.props` (repo root) — shared MSBuild props.
  - `Directory.Packages.props` (repo root) — central package versions.
  - `src\WhereToEat.sln` — solution with solution folders `00-Shared`, `01-Modules`,
    `02-Hosts`, `03-Tests`.
  - `src\Shared\WhereToEat.SharedKernel\WhereToEat.SharedKernel.csproj`
  - `src\Shared\WhereToEat.SharedKernel\Results\Result.cs`, `Results\Error.cs`,
    `Results\ResultExtensions.cs`
  - `src\Shared\WhereToEat.SharedKernel\ValueObjects\Money.cs`, `ValueObjects\GeoPoint.cs`
  - `src\Shared\WhereToEat.SharedKernel\Geo\Haversine.cs`
  - `src\Shared\WhereToEat.SharedKernel\Primitives\ValueObject.cs` (base equality),
    `Primitives\Entity.cs`, `Primitives\AggregateRoot.cs`, `Primitives\IDomainEvent.cs`
  - `src\Shared\WhereToEat.Contracts\WhereToEat.Contracts.csproj`
  - `src\Shared\WhereToEat.Contracts\IntegrationEvents\MenuUpdated.cs`,
    `IntegrationEvents\RatingGiven.cs`, `IntegrationEvents\AnalyticsEventRaised.cs`
  - `src\Shared\WhereToEat.Contracts\Recommendation\RecommendationRequest.cs`,
    `Recommendation\RecommendationResultDto.cs`
- **acceptance criteria:**
  - `dotnet build src\WhereToEat.sln` succeeds with zero warnings.
  - `Result.Failure(error)` carries the `Error`; `Result<T>.Success(value)` exposes `Value`;
    accessing `Value` on a failed result throws.
  - `Money` rejects mismatched-currency arithmetic and negative amounts where invalid; equality
    is by value.
  - `GeoPoint` rejects latitude outside [-90,90] and longitude outside [-180,180].
  - `Haversine.DistanceKm(a, b)` for two known coordinate pairs returns the expected great-circle
    distance within 0.5% tolerance; `Haversine.DistanceKm(a, a) == 0`.
  - SharedKernel and Contracts reference **no** ASP.NET, EF, Dapper, or infrastructure packages.
- **testing requirements:** Unit test project `tests\Unit\WhereToEat.SharedKernel.UnitTests`
  covering: `Result`/`Error` happy + failure paths; `Money` equality + invalid-operation guards;
  `GeoPoint` validation bounds; `Haversine` against at least three known city-pair distances
  (e.g., Kyiv↔Lviv) and the zero-distance identity. All tests pass via `dotnet test`.

### Step 2: Architecture-fitness test project (NetArchTest) — boundary guards first

- **status:** [x] done
- **description:** Stand up the architecture-fitness test project **early** so every later step
  is mechanically guarded against boundary erosion (the design's mitigation for modular-monolith
  drift). Encode the dependency rules from this plan's conventions as failing tests. Because most
  module assemblies do not exist yet, write the rules to **discover assemblies by naming
  convention at runtime** (load all `WhereToEat.*` assemblies from the build output and assert
  against whatever is present), so the same tests grow automatically as modules are added.
  Implement the **module-grouping helper** from this plan's conventions: an assembly's "module"
  is its first namespace segment after `WhereToEat` (`Catalog`, `Recommendation`, `Ratings`, …),
  with the single special case that **`WhereToEat.Catalog.Search.*` resolves to the `Catalog`
  module** (search is a Catalog sub-module, not a module of its own); shared families
  (`SharedKernel`, `Contracts`, `BuildingBlocks.*`) and `Host` assemblies are not modules. The
  cross-module rules (d/e below) use this helper, so an intra-`Catalog` reference between
  `Catalog.*` and `Catalog.Search.*` is **not** flagged. Rules to assert: (a) any `*.Domain`
  assembly depends on nothing in `WhereToEat.*` except `WhereToEat.SharedKernel`; (b) any
  `*.Application` assembly does not depend on any `*.Infrastructure` or any `Host`; (c) EF Core
  (`Microsoft.EntityFrameworkCore*`) is referenced **only** by `WhereToEat.Admin.Infrastructure`;
  (d) no module's non-`Contracts` assembly is referenced by a **different** module's assembly
  (per the module-grouping helper — `Catalog.Search.*` ↔ `Catalog.*` is same-module and allowed);
  (e) **MassTransit consumers** (types implementing `IConsumer<>`) depend only on
  `WhereToEat.Contracts` types across module lines, never on another module's
  `Domain`/`Application` internals (the design calls this out explicitly). Document each rule with
  a comment naming the invariant/decision it enforces.
- **files to modify:**
  - `tests\Architecture\WhereToEat.ArchitectureTests\WhereToEat.ArchitectureTests.csproj`
    (references NetArchTest.Rules, xUnit, FluentAssertions)
  - `tests\Architecture\WhereToEat.ArchitectureTests\AssemblyLoader.cs` (convention-based loader)
  - `tests\Architecture\WhereToEat.ArchitectureTests\ModuleGrouping.cs` (module-grouping helper +
    the `Catalog.Search.*` → `Catalog` special case)
  - `tests\Architecture\WhereToEat.ArchitectureTests\ModuleGroupingTests.cs`
  - `tests\Architecture\WhereToEat.ArchitectureTests\LayerDependencyTests.cs`
  - `tests\Architecture\WhereToEat.ArchitectureTests\ModuleBoundaryTests.cs`
  - `tests\Architecture\WhereToEat.ArchitectureTests\EfCoreContainmentTests.cs`
  - `tests\Architecture\WhereToEat.ArchitectureTests\MessageConsumerBoundaryTests.cs`
- **acceptance criteria:**
  - `dotnet test tests\Architecture\WhereToEat.ArchitectureTests` passes against the current
    (Step 1) solution (SharedKernel/Contracts comply).
  - A deliberately-introduced violation (e.g., a temporary reference from SharedKernel to an
    infrastructure package, reverted afterward) makes a fitness test **fail** — proving the guard
    actually fires. (Document this manual verification in the PR description.)
  - The EF-containment test exists and is wired even though Admin.Infrastructure does not exist
    yet (it passes vacuously until Step 10 adds EF).
  - The **module-grouping helper** resolves `WhereToEat.Catalog.Search.Application` (and any
    `WhereToEat.Catalog.Search.*`) to the `Catalog` module, and resolves
    `WhereToEat.Recommendation.*` to `Recommendation` — so the cross-module rules never flag a
    `Catalog.Search.*` ↔ `Catalog.*` reference but still flag a true cross-module leak.
- **testing requirements:** The architecture tests **are** the test artifact; they must run green
  in CI on every subsequent step. Add a CI note that these tests are non-skippable. Include the
  documented negative check (introduce-then-revert a violation) as the proof the rules bite. Add
  a focused unit test (`ModuleGroupingTests`) asserting the module-grouping helper's mapping,
  including the `Catalog.Search.*` → `Catalog` special case and a normal `Recommendation.*` →
  `Recommendation` case, so the Search naming decision is mechanically pinned.

### Step 3: Catalog domain core — two-level taxonomy ER model + invariants

- **status:** [x] done
- **description:** Implement the persistence-free **Catalog domain** for the core ER model — this
  same Catalog domain backs the `Catalog.Search` sub-module (Step 6 adds only its Application
  layer; there is no separate Search domain project)
  (CLAUDE.md invariant #2): `Category` (a *group* of dishes), `Dish` (a concrete position
  belonging to exactly one Category, carrying the canonical normalized name), `Restaurant`
  (aggregate root owning Address, Coordinates, ContactLinks, and MenuItems), `MenuItem` (the
  *dish-at-restaurant* join carrying `Money` price, weight/quantity, a `SourceKind`
  parsed-vs-admin enum, and the **`DoNotParse` flag**), `Address` (value object, admin-editable,
  raises a domain event on change to trigger re-geocode), `Coordinates` (value object wrapping
  `GeoPoint` + optional Google **Place ID** + Maps deep-link — and a guard that **forbids
  constructing it from a "Google" source**, encoding invariant #7 in the model), and
  `ContactLinks` (website/social links, always-shown/never-monetized). Encode invariants as
  constructor/factory guards returning `Result`: a Dish must belong to a Category; a MenuItem
  must reference an existing Dish and Restaurant; a Restaurant cannot have two MenuItems for the
  same Dish; price must be non-negative `Money`. Define the `RestaurantId`, `DishId`,
  `CategoryId`, `MenuItemId` strongly-typed IDs. **No EF/Dapper/ASP.NET references** in this
  project.
- **files to modify:**
  - `src\Modules\Catalog\WhereToEat.Catalog.Domain\WhereToEat.Catalog.Domain.csproj`
  - `...\Catalog.Domain\Taxonomy\Category.cs`, `Taxonomy\Dish.cs`
  - `...\Catalog.Domain\Restaurants\Restaurant.cs`, `Restaurants\MenuItem.cs`,
    `Restaurants\Address.cs`, `Restaurants\Coordinates.cs`, `Restaurants\ContactLink.cs`,
    `Restaurants\SourceKind.cs`
  - `...\Catalog.Domain\Identifiers\` (strongly-typed ID structs)
  - `...\Catalog.Domain\Events\AddressChanged.cs`, `Events\MenuItemUpserted.cs`
  - `...\Catalog.Domain\Abstractions\ICatalogRepository.cs` (port — Domain-shaped read/write
    contract; no SQL leaks)
- **acceptance criteria:**
  - `dotnet build` succeeds; the project references only `WhereToEat.SharedKernel`.
  - Factory guards: creating a `Dish` without a `CategoryId` returns a failure `Result`; adding a
    second `MenuItem` for an existing Dish on a `Restaurant` returns failure; negative price is
    rejected.
  - `Coordinates` cannot be created via a "from Google coords" path (no such factory exists; the
    only spatial factory accepts OSM/Nominatim-sourced `GeoPoint`); `PlaceId` is an allowed
    optional field.
  - Mutating an `Address` raises an `AddressChanged` domain event on the aggregate.
- **testing requirements:** Unit tests in `tests\Unit\WhereToEat.Catalog.Domain.UnitTests`:
  taxonomy invariants (Dish↔Category, one-MenuItem-per-Dish), price/Money guards, the
  Coordinates "no Google geo" guard, and `AddressChanged` emission. Architecture tests from Step 2
  must still pass (Catalog.Domain depends only on SharedKernel).

### Step 4: SQL schema migration scripts, migration runner, and the Dapper data layer (with spatial)

- **status:** [x] done
- **description:** Make the catalog domain persistable. Choose **DbUp** as the migration runner
  (lightweight, script-first, no schema generation — consistent with "SQL scripts are the
  schema-of-record"); use it consistently for all future schema steps. Author the **versioned SQL
  scripts** that create the catalog tables on **MS SQL Server**: `Category`, `Dish`,
  `Restaurant`, `MenuItem`, `Address`, `ContactLink`, plus the restaurant **`geography`
  coordinate column with a spatial index** (`CREATE SPATIAL INDEX` for index-assisted radius
  pre-filter) and the Place-ID/deep-link columns (no raw Google lat/lng column exists at all).
  Use a **JSON column** for the low-churn raw-parsed snapshot on `Restaurant`/`MenuItem` (per the
  design's JSON-for-low-churn note). Build a `BuildingBlocks.Persistence` library wrapping a
  Dapper connection factory and a tiny query/command helper, and implement
  `WhereToEat.Catalog.Infrastructure` with a Dapper-backed `ICatalogRepository` whose hot-path
  queries are hand-written SQL — including a **radius pre-filter** that uses the spatial index
  (`geography::STDistance` / `.STBuffer` or `STIntersects`) to narrow candidates, after which
  exact distance is computed in code via `Haversine` (the spatial index narrows, Haversine
  decides — invariant #7). Provide a console **migration-runner** entry (used locally and by the
  worker/host startup) that applies pending scripts idempotently.
- **files to modify:**
  - `db\migrations\0001_create_taxonomy.sql` (Category, Dish)
  - `db\migrations\0002_create_restaurant_and_address.sql` (Restaurant + geography column +
    spatial index, Address, ContactLink)
  - `db\migrations\0003_create_menuitem.sql` (MenuItem + DoNotParse flag + SourceKind + JSON
    raw-snapshot column)
  - `src\Shared\WhereToEat.BuildingBlocks.Persistence\WhereToEat.BuildingBlocks.Persistence.csproj`
    (Dapper, Microsoft.Data.SqlClient)
  - `...\BuildingBlocks.Persistence\ISqlConnectionFactory.cs`, `SqlConnectionFactory.cs`
  - `src\Shared\WhereToEat.BuildingBlocks.Migrations\` (DbUp runner library) +
    `tools\WhereToEat.MigrationRunner\` (console host that points DbUp at `db\migrations`)
  - `src\Modules\Catalog\WhereToEat.Catalog.Infrastructure\WhereToEat.Catalog.Infrastructure.csproj`
  - `...\Catalog.Infrastructure\Persistence\DapperCatalogRepository.cs`,
    `Persistence\Sql\` (named SQL query strings), `Persistence\Mappings\` (row→domain mappers)
- **acceptance criteria:**
  - Running the migration runner against a fresh SQL Server applies all scripts; re-running is a
    no-op (idempotent journal table).
  - The `Restaurant` coordinate column is SQL `geography`; `sys.indexes` shows a spatial index on
    it. **No** raw Google lat/lng column exists.
  - `DapperCatalogRepository` round-trips a `Restaurant` + `Dish` + `MenuItem` (insert then read
    reconstructs the aggregate with correct `Money`, Place ID, coordinates).
  - The radius pre-filter query returns only restaurants whose `geography` lies within the given
    radius; exact ordering/distance is then produced by `Haversine` in code.
  - Only `WhereToEat.Admin.Infrastructure` is permitted EF references — this project uses Dapper
    only (Step 2 EF-containment test still green).
- **testing requirements:** **Containerized-SQL integration tests** in
  `tests\Integration\WhereToEat.Catalog.Integration` using **Testcontainers (SQL Server image)**:
  (1) migration runner creates the schema and the spatial index; (2) repository round-trip of the
  catalog aggregate; (3) radius pre-filter returns the correct candidate set for a seeded set of
  coordinates and a known radius, and code-side `Haversine` orders them correctly. Tests are
  CI-stable (container spun per fixture, torn down after).

### Step 5: Remaining domain entities & modules with per-module persistence

- **status:** [x] done

- **description:** Implement the remaining domain modules from the design's ER model, each with
  its own Domain project and the persistence the design assigns it (Dapper for hot/read paths,
  EF deferred to Admin in Step 10). **Ratings module:** `Rating` (a user's score for a
  restaurant) and the per-restaurant aggregate carrying cumulative all-time sum + count and the
  **Bayesian additive-prior smoothed value** `smoothed = (C·m + Σ) / (C + n)` — implement this as
  pure domain math in `Ratings.Domain` with `C` (prior weight) and `m` (global mean) injected as
  **configurable** values; this is the single authoritative formula `f_quality` later consumes.
  **Users:** minimal `User` aggregate (external-IdP subject id + role mapping; PII minimized,
  never venue-exposed). **Photos:** `Photo` with a `IsGeneric`/`PermissionGranted` gate so the
  **generic-category photo is the default on every dish** and real photos require permission
  (invariant #8). **VerifiedStatus/Subscription:** status + tier (Basic/Pro), future-facing.
  **Promotion/AdPlacement:** a labeled, separate placement that is **never** a rank modifier
  (invariant #10) — model carries an explicit `IsLabeledAd` truth and no link to organic score.
  **RestaurantProtection:** the restaurant-level **DoNotUpdate** flag set (invariant #3),
  complementing the per-MenuItem `DoNotParse` from Step 3. **AnalyticsEvent:** the anonymized
  event aggregate with the retain/hash/drop shape (coarse geohash, hour-truncated time,
  dish/category ids, sort/filter, position, rotating-salted id hash) — domain shape only; ingest
  pipeline is Step 11. Add SQL migration scripts + Dapper repositories for the read/write paths
  these modules need (ratings aggregate read is a hot path; analytics is append-optimized).
- **files to modify:**
  - `src\Modules\Ratings\WhereToEat.Ratings.Domain\` (`Rating.cs`,
    `RatingAggregate.cs`, `BayesianSmoothing.cs`, `RatingSmoothingOptions.cs`)
  - `src\Modules\Ratings\WhereToEat.Ratings.Infrastructure\` (Dapper repo + aggregate read)
  - `src\Modules\Users\WhereToEat.Users.Domain\` (`User.cs`, role mapping value objects)
  - `src\Modules\Catalog\WhereToEat.Catalog.Domain\Photos\Photo.cs` (Photo belongs with
    dish/restaurant taxonomy) + permission gate
  - `src\Modules\Monetization\WhereToEat.Monetization.Domain\` (`VerifiedStatus.cs`,
    `SubscriptionTier.cs`, `Promotion.cs`, `AdPlacement.cs`) — fleshed-out behavior in Step 14
  - `src\Modules\Admin\WhereToEat.Admin.Domain\Protection\RestaurantProtection.cs`
  - `src\Modules\Analytics\WhereToEat.Analytics.Domain\` (`AnalyticsEvent.cs`, `EventKind.cs`,
    `Geohash.cs`, anonymization value objects)
  - `db\migrations\0004_create_ratings.sql`, `0005_create_users.sql`,
    `0006_create_photos.sql`, `0007_create_protection_flags.sql`,
    `0008_create_monetization.sql`, `0009_create_analytics_events.sql`
- **acceptance criteria:**
  - `BayesianSmoothing.Smooth(sum, count, C, m)` equals raw average as `count→∞` and equals `m`
    when `count==0`; a single 5-star review with default `C` stays near `m`, while hundreds of
    4.5 ratings approach 4.5 (proving the smoothing intent).
  - `Photo` defaults to generic and cannot expose a real photo unless `PermissionGranted` is true.
  - `AdPlacement`/`Promotion` carry an explicit labeled-ad marker and have **no** field that
    feeds organic ranking (compile-time absence verifiable by inspection + unit test on the type's
    public surface).
  - `RestaurantProtection.DoNotUpdate` and `MenuItem.DoNotParse` are both queryable for the parser
    persist gate.
  - All new migration scripts apply idempotently on top of Step 4's schema.
  - Each module's Domain references only SharedKernel; architecture tests stay green.
- **testing requirements:** Unit tests per module: **Ratings** — exhaustive `BayesianSmoothing`
  cases (zero count, one review, large count, boundary `C` values, configurable `m`); **Photos** —
  permission gate; **Monetization** — labeled-ad invariant; **Analytics** — anonymization value
  object construction. Integration tests (Testcontainers SQL) for the Ratings aggregate read and
  the analytics append path round-trips.

### Step 6: Public API host skeleton — Catalog/Search endpoints + OIDC + composition root

- **status:** [x] done
- **description:** Create the **public-API host** as its own composition root (ASP.NET Core
  Minimal API or controllers — use Minimal API for the read endpoints). Wire the **Catalog
  Application** layer (use-case handlers for: list categories, list dishes within a category,
  get restaurant details — details **always** include site/social `ContactLinks` for free, even
  for non-Verified venues, per §5.8) and the **Catalog.Search Application** sub-module
  (`WhereToEat.Catalog.Search.Application` — deterministic list/lookup of categories and dishes to
  build the user's selection set — **no NLP**, pure list/filter queries; it is a sub-module of
  Catalog per this plan's conventions, so it may reference `Catalog.*` freely). Add the **OIDC
  resource-server** auth wiring: configure JWT bearer validation against the external IdP's
  authority/JWKS, and an `IIdentityContext`/claims-to-user-role mapping abstraction (the identity
  seam so the IdP is swappable). Public read endpoints are anonymous; the seam is in place for
  authenticated endpoints (ratings in a later step). Add health checks, Swagger, and a
  `CompositionRoot`/`ServiceCollection` extension per module that the host calls
  (`AddCatalogModule`, `AddCatalogSearchModule`).
  **Map endpoint — DB-only stub this step.** Map a Map endpoint that returns the Google Maps
  **Embed**/Place ID/deep-link payload built **only from fields already stored on the restaurant**
  (the stored `geography` coordinates, the optional Place ID, and the deep-link) — live-only,
  **nothing cached** (§5.7), and **no geocoder port involved** (the `IGeocoder` port and the
  `AddressChanged`→re-geocode wiring arrive in **Step 8**, so Step 6 stays independently
  buildable). The endpoint reads through the existing catalog repository; it never geocodes and
  never returns a Google rating or any Google-sourced coordinate. (Wiring the AddressChanged event
  to a re-geocode is **out of scope here** and lives entirely in Step 8.)
- **files to modify:**
  - `src\Modules\Catalog\WhereToEat.Catalog.Application\` (`ListCategoriesQuery`,
    `ListDishesByCategoryQuery`, `GetRestaurantDetailsQuery`, `GetRestaurantMapPayloadQuery`
    (reads stored coordinates / Place ID / deep-link only) + handlers, `ICatalogRepository`
    consumption)
  - `src\Modules\Catalog\WhereToEat.Catalog.Search.Application\` (deterministic lookup queries)
  - `src\Hosts\WhereToEat.PublicApi\WhereToEat.PublicApi.csproj`
  - `...\PublicApi\Program.cs` (composition root), `Endpoints\CatalogEndpoints.cs`,
    `Endpoints\SearchEndpoints.cs`, `Endpoints\MapEndpoints.cs`
  - `src\Shared\WhereToEat.BuildingBlocks.Auth\` (`IIdentityContext.cs`, OIDC JWT bearer
    extension, claims→role mapper)
  - `...\Catalog.Infrastructure\DependencyInjection\AddCatalogModule.cs`
  - `appsettings.json` + `appsettings.Development.json` for the public host (IdP authority, conn
    string, Redis placeholder)
- **acceptance criteria:**
  - `GET /categories` returns the category list; `GET /categories/{id}/dishes` returns dishes in
    that category; `GET /restaurants/{id}` returns details that **always** include `ContactLinks`.
  - Search endpoints accept selection-building list/lookup queries and contain **no** free-text
    NLP parsing (only id/prefix list filters); they are served by
    `WhereToEat.Catalog.Search.Application` and the Step 2 fitness tests stay green (the
    `Catalog.Search.*` ↔ `Catalog.*` reference is same-module and allowed).
  - The host validates a JWT from the configured IdP authority and rejects an invalid/expired
    token with 401; anonymous read endpoints remain reachable.
  - `GET /map/{restaurantId}` returns the Embed/Place-ID/deep-link payload built **only from the
    restaurant's stored fields** (stored `geography` coordinates, optional Place ID, deep-link),
    with **no** cached Google rating or coordinates and **no** geocoder call (the geocoder port
    does not exist until Step 8). For a restaurant with no stored coordinates the endpoint returns
    a well-defined "no map payload" response rather than attempting to geocode.
  - The host has **no** dependency on a Geo/geocoder port at this step; the `AddressChanged`→
    re-geocode wiring is intentionally absent here (added in Step 8).
  - `dotnet run` on the host boots, applies/links to the DB, and serves Swagger.
- **testing requirements:** Integration tests with `WebApplicationFactory` (Testcontainers SQL
  for data) asserting: catalog/search endpoint contracts and that restaurant details include
  contact links; that `GET /map/{restaurantId}` returns a payload assembled purely from stored
  DB fields (seed a restaurant with coordinates + Place ID, assert the payload echoes them, and
  assert no geocoder is invoked — there is none to invoke) and returns the "no map payload"
  response for a restaurant without stored coordinates; an unauthenticated request to a protected
  route returns 401 while public reads return 200 (use a test JWT signing key / fake authority for
  the auth test). Application-layer unit tests for the catalog/search/map handlers with a mocked
  repository.

### Step 7: Recommendation module — pluggable match/aggregate/rank/filter/sort + Recommend endpoint

- **status:** [x] done
- **description:** Implement the 5-step recommendation pipeline from CLAUDE.md §6 as **pluggable,
  DI-by-key strategies** (invariant #4). **Step 1 match predicates:** `IMatchStrategy` with `OR`
  (≥1 selected item present) and `AND` (all present — the combo search) implementations, plus the
  seam for future `K-of-N`; resolved by a `match` key. **Step 2 aggregation:** per-restaurant
  basket price (cheapest matched for OR, sum for AND), quality (the **Step 5 Bayesian smoothed
  rating**), distance (`Haversine`), and **coverage** (count of selected items present).
  **Cross-module rating read — via the candidate-source DTO, not the Ratings domain.** The
  recommendation engine reads the smoothed rating as a **plain field on the candidate-source
  result DTO** returned by `IRecommendationCandidateSource` (e.g.,
  `RecommendationCandidate.SmoothedRating` / review count) — it consumes that pre-computed value
  and does **not** re-derive it and does **not** reference the Ratings module's domain types. The
  infrastructure candidate source is the only place that joins the materialized rating aggregate
  (owned by the Ratings module via the DB, never via a code reference into `Ratings.Domain`), so
  the Step 2 cross-module fitness tests stay green. **Step 3 ranking — two cases, encoded
  distinctly:**
  (A) **explicit sort** (`price`/`distance`/`rating`) makes that field the **primary key** and
  uses **coverage strictly as a tie-breaker** (invariant #5 — cheapest wins even at 1-of-3;
  coverage only breaks ties between equal primary values); (B) **composite mode**
  (`price-quality`/`best`) computes a normalized `score = f_price ⊗ f_quality ⊗ f_distance ⊗
  f_coverage` with **per-mode weights** and sorts by it. Each `f_*` is a normalized (0…1)
  function: `f_price` reads the **precomputed per-area/category median** through an
  `IPriceMedianProvider` port (city-wide fallback below threshold N — Step 12's nightly job
  populates the median **table**); in this step the provider implementation **reads the median
  table directly from the DB (DB-only, no Redis)**, so Step 7 is buildable/testable before any
  caching exists — Redis is added later in **Step 13** as a *transparent caching layer in front
  of the same `IPriceMedianProvider`*, not a change to this port or its callers. `f_quality`
  reads the smoothed rating field carried on the candidate DTO (above), `f_distance` is
  distance-normalized (on by default, user-disable-able), `f_coverage` from coverage. **Step 4
  filters:** composable
  `IResultFilter` modules (price, rating; seam for open-now/vegan/delivery). **Step 5:** final
  ordered/filtered list. Register strategies/filters by key in DI so new modes self-register
  without touching existing ones. Add the `POST /recommend` endpoint taking
  `{ items, match, sort, filters, userGeo }`.
- **files to modify:**
  - `src\Modules\Recommendation\WhereToEat.Recommendation.Domain\`
    (`IMatchStrategy.cs`, `OrMatchStrategy.cs`, `AndMatchStrategy.cs`,
    `IScoringFunction.cs`, `FPrice.cs`, `FQuality.cs`, `FDistance.cs`, `FCoverage.cs`,
    `ISortStrategy.cs`, `ExplicitSortStrategy.cs`, `CompositeScoreStrategy.cs`,
    `IResultFilter.cs`, `RestaurantCandidate.cs`, `RecommendationPipeline.cs`,
    `RecommendationModeOptions.cs` for per-mode weights/threshold N)
  - `src\Modules\Recommendation\WhereToEat.Recommendation.Application\`
    (`RecommendQuery` + handler orchestrating the 5 steps via the resolved strategies,
    `IPriceMedianProvider` port, `IRecommendationCandidateSource` port + its result DTO
    `RecommendationCandidate` carrying the smoothed-rating field — so the engine never references
    `Ratings.Domain`)
  - `src\Modules\Recommendation\WhereToEat.Recommendation.Infrastructure\`
    (Dapper candidate source hitting the catalog read model + the radius pre-filter and joining the
    **materialized rating aggregate** into the candidate DTO's smoothed-rating field — DB join
    only, no `Ratings.Domain` reference; `DbPriceMedianProvider` reading the precomputed median
    **table directly (DB-only — no Redis here)**; strategy-by-key DI registration)
  - `db\migrations\0010_create_price_medians.sql` — **creates** the per-area/category price-median
    table here (so the DB-only `DbPriceMedianProvider` and the Step 7 integration test can read/seed
    it independently of the Step 12 worker). Step 12's nightly job only **populates/refreshes** this
    table; it is created in this step.
  - `...\PublicApi\Endpoints\RecommendEndpoints.cs`
- **acceptance criteria:**
  - **Explicit-sort-dominates is provable:** with `sort=price`, a 1-of-3 cheaper venue ranks above
    a 3-of-3 pricier venue; with two equal-price venues, the higher-coverage one ranks first
    (tie-breaker only). Equivalent tests for `sort=distance` and `sort=rating`.
  - **Composite score:** `price-quality` and `best` produce a deterministic ordering from the
    weighted normalized `f_*`; changing a mode weight changes ordering as expected.
  - `OR` selects venues with ≥1 selected item; `AND` selects only venues with all selected items;
    coverage is computed correctly for both.
  - A new match/sort/filter can be added by registering a keyed strategy **without** editing the
    pipeline or existing strategies (demonstrated by adding a trivial `K-of-N`-stub test strategy
    in tests).
  - `f_quality` consumes the **smoothed-rating field on the candidate DTO** (no reference into
    `Ratings.Domain`); `f_price` reads a precomputed median via the `IPriceMedianProvider` port,
    whose Step 7 implementation is **DB-only** (reads the median table, no Redis — Redis arrives in
    Step 13 transparently behind the same port). No per-request median computation occurs.
  - The **Step 2 cross-module fitness test stays green**: `WhereToEat.Recommendation.*` has **no**
    reference to `WhereToEat.Ratings.*` (the rating value crosses only as a DTO field via the DB
    join), and the median provider is DB-only with no Step 13 caching dependency.
- **testing requirements:** Unit tests are the core gate: a table-driven suite proving
  explicit-sort-dominates with coverage-as-tie-breaker across price/distance/rating; composite
  score ordering and weight sensitivity; OR/AND candidate selection and coverage; the
  pluggability test (register a test strategy by key, resolve it, no pipeline change); and a test
  feeding candidate DTOs with a pre-set smoothed-rating field (no `Ratings.Domain` involved)
  proving `f_quality`/`rating` sort consume that field.
  Integration test for `POST /recommend` against a seeded catalog (Testcontainers SQL) asserting
  end-to-end ordering for one explicit and one composite mode. **Median-data independence (Step 7
  predates the Step 12 worker job):** the integration test must **not** depend on the nightly job
  having run — it either **seeds the price-median table directly** (insert known per-area/category
  medians before the call) or registers a **stub `IPriceMedianProvider`** returning fixed medians,
  so the composite-mode (`f_price`) test runs deterministically before Step 12 exists. Likewise the
  smoothed-rating used by the candidate source is **seeded** into the materialized aggregate (or a
  stub candidate source supplies it), so the test does not depend on the Step 12 rating-recompute
  job either.

### Step 8: Geo module — OSM/Nominatim geocoder port + adapter + distance

- **status:** [x] done
- **description:** Implement the **Geo module**: an `IGeocoder` port (address → `GeoPoint`) in
  `Geo.Application` and an OSM/**Nominatim** adapter in `Geo.Infrastructure` that calls Nominatim
  over HTTP, respects its usage policy (User-Agent, rate-limit, attribution), and returns
  coordinates we are allowed to store under ODbL. The adapter maps a successful response into our
  `Coordinates` value object (storing the OSM-sourced `GeoPoint` and optional Place ID separately
  — **never** Google lat/lng). Expose a distance helper that reuses `SharedKernel.Haversine` for
  display/ranking. **Own the `AddressChanged`→re-geocode seam end-to-end here** (Step 6
  deliberately omits it): provide the application use-case `GeocodeRestaurantCommand` and the
  **`AddressChanged` → geocode wiring** — an in-module handler/consumer that, on an `AddressChanged`
  event (raised by the Step 3 catalog aggregate), enqueues/invokes the geocode for that restaurant.
  The Step 12 worker job drives this on a schedule/queue, and Step 13 carries the event over the
  bus; **the geocoder port, the command, and the AddressChanged handler all first appear in this
  step** (nothing before Step 8 references `IGeocoder`). Add a typed `HttpClient` with a polite
  default rate-limit and resilience (timeouts/retries via `Microsoft.Extensions.Http`/Polly).
- **files to modify:**
  - `src\Modules\Geo\WhereToEat.Geo.Application\` (`IGeocoder.cs`, `GeocodeRestaurantCommand.cs`
    + handler, `GeocodeOnAddressChangedHandler.cs` — the AddressChanged→re-geocode wiring that
    Step 6 deferred to here)
  - `src\Modules\Geo\WhereToEat.Geo.Infrastructure\` (`NominatimGeocoder.cs`,
    `NominatimOptions.cs`, `NominatimResponse.cs`, typed `HttpClient` + Polly policy,
    `AddGeoModule.cs`)
  - `appsettings` entries for the Nominatim base URL, User-Agent, and rate-limit.
- **acceptance criteria:**
  - `NominatimGeocoder.Geocode(address)` returns a `Result<Coordinates>` whose `GeoPoint` is
    within tolerance of a known address's coordinates (asserted against a recorded/mocked HTTP
    response, not the live service in CI).
  - The adapter sets a descriptive User-Agent and honors a configured minimum request interval
    (rate-limit) — verified by a fake handler counting/timing requests.
  - The produced `Coordinates` stores OSM-sourced geo + optional Place ID; there is **no** code
    path that writes Google lat/lng.
  - `GeocodeRestaurantCommand` updates a restaurant's coordinates and persists them as the SQL
    `geography` value (via the catalog repository).
  - The **`AddressChanged`→re-geocode seam** (deferred from Step 6) works: handling an
    `AddressChanged` event for a restaurant triggers `GeocodeRestaurantCommand` and the
    restaurant's stored coordinates are refreshed. (This is the seam the Step 6 Map endpoint stub
    intentionally did not wire; it now exists in Step 8.)
- **testing requirements:** Unit/integration tests with a **mocked `HttpMessageHandler`** serving
  a pinned Nominatim JSON fixture (no live calls in CI): success mapping, rate-limit interval
  enforcement, and error/empty-result handling returning a failure `Result`. An integration test
  persisting the geocoded coordinate into the `geography` column (Testcontainers SQL). A test that
  feeding an `AddressChanged` event invokes the geocode and refreshes the stored coordinates
  (handler-level test with a mocked geocoder + catalog repository).

### Step 9: Parsing module — strategy abstraction + compliance + admin-protection + Selenium/library strategies + e2e/integration harness

- **status:** [x] done
- **description:** Implement the pluggable **parser subsystem** (the design's primary verification
  path) end-to-end as a working skeleton. Define `IRestaurantParser` (input: source descriptor;
  output: a **normalized menu contract** of `{restaurant facts, list of {raw dish name, price,
  weight, category hint}}`) and a `IParserStrategy` selection abstraction registered by key, with
  a **seam interface for a future Puppeteer locator strategy** (declared, not implemented).
  Implement two concrete strategies behind the same contract: a **Selenium-based** strategy
  (`Selenium.WebDriver` + a `IWebDriverFactory` seam so the e2e harness can inject a headless
  driver) for JS-rendered sites, and a **library-based** strategy using **AngleSharp** for static
  HTML. **Compliance gates run before any fetch** (invariant #9): a robots.txt checker, a
  per-host **rate-limiter** (Redis-counter-backed via the cache abstraction, in-memory fallback),
  and a **first-party-only** allow-list guard (reject aggregators). The **normalize** step maps
  raw dish names to the canonical Category→Dish taxonomy and unifies units/prices.
  **Unmappable dishes go to a quarantine / review queue (not dropped, not fatal).** When a raw
  parsed dish name **cannot be mapped** to a canonical `Dish`, the normalizer does **not** silently
  drop it and does **not** fail the whole parse: it routes the raw item (raw name, price, weight,
  category hint, source restaurant) into a **quarantine / review queue** (a `ParseQuarantineItem`
  persisted to a quarantine table) for later admin resolution (the admin module surfaces this
  queue in a future step), while every **mappable** item in the same menu still normalizes and
  persists normally. The persist step never writes a quarantined item as a live `MenuItem` until
  an admin maps it. The **persist** step also enforces **admin protection before writing**
  (invariant #3): skip any `Restaurant` flagged `DoNotUpdate` and any `MenuItem` flagged
  `DoNotParse`. Provide **at least one concrete example restaurant parser of each type** (one
  Selenium example, one AngleSharp example) wired to a pinned HTML fixture. Build the **shared e2e
  harness** so "new restaurant = new test class + fixture" is the only work.
- **files to modify:**
  - `src\Modules\Parsing\WhereToEat.Parsing.Domain\` (`IRestaurantParser.cs`,
    `ParsedMenu.cs` normalized contract, `ParsedDish.cs`, `SourceDescriptor.cs`,
    `INormalizer.cs`, `NormalizationResult.cs` (mapped items + unmapped/quarantined items),
    `ParseQuarantineItem.cs`, `IPuppeteerLocatorStrategy.cs` seam)
  - `src\Modules\Parsing\WhereToEat.Parsing.Application\` (`RunParseCommand.cs` + handler:
    compliance → fetch → normalize (mapped persist + unmapped→quarantine) → geocode →
    admin-protected persist; `IRobotsTxtGate.cs`, `IHostRateLimiter.cs`, `IFirstPartyAllowList.cs`,
    `IParseQuarantineStore.cs` port)
  - `src\Modules\Parsing\WhereToEat.Parsing.Infrastructure\`
    (`SeleniumParserStrategy.cs`, `IWebDriverFactory.cs`, `AngleSharpParserStrategy.cs`,
    `RobotsTxtGate.cs`, `RedisHostRateLimiter.cs`, `Normalizer.cs`,
    `DapperParseQuarantineStore.cs`,
    `Examples\BorschCafeSeleniumParser.cs`, `Examples\PizzaHouseAngleSharpParser.cs`,
    `AddParsingModule.cs`)
  - `db\migrations\0011_create_parse_quarantine.sql` (quarantine / review-queue table for
    unmappable parsed dishes; numbered before the later steps' scripts since the parser builds
    first and DbUp applies scripts in numeric order)
  - `tests\E2E\WhereToEat.Parsing.E2E\` (shared `SeleniumParserHarness.cs`, fixtures under
    `Fixtures\borsch-cafe\menu.html`, and `BorschCafeParserTests.cs`)
  - `tests\Integration\WhereToEat.Parsing.Integration\` (library-parser integration tests over
    `Fixtures\pizza-house\menu.html`, `PizzaHouseParserTests.cs`)
- **acceptance criteria:**
  - Both example parsers produce the **same normalized `ParsedMenu` contract** from their
    fixtures (dish name → canonical Dish, price as `Money`, weight, category hint).
  - Compliance gates fire **before** fetch: a robots.txt disallow blocks parsing; the rate-limiter
    enforces the per-host interval; a non-first-party (aggregator) host is rejected.
  - Admin protection holds: a `Restaurant` flagged `DoNotUpdate` is skipped entirely; within an
    updatable restaurant, a `MenuItem` flagged `DoNotParse` is **not** created/overwritten.
  - **Unmappable dish → quarantine, not drop, not fail:** when a raw parsed dish name cannot be
    mapped to a canonical `Dish`, the normalizer routes that item to the **quarantine / review
    queue** (`ParseQuarantineItem` persisted to the quarantine table) for later admin resolution;
    it is **not** silently dropped and it does **not** fail the parse. Every mappable item in the
    **same menu still normalizes and persists** as a live `MenuItem`, and the quarantined item is
    **not** written as a live `MenuItem` until an admin maps it.
  - A new restaurant parser can be added as a strategy + fixture + test class with **no** engine
    changes (demonstrated by the second example reusing the harness).
  - The Puppeteer locator is a declared seam interface with **no** implementation (non-goal).
- **testing requirements:** **This is the main parser-correctness gate.** (1) **Per-restaurant
  Selenium e2e** test (`BorschCafeParserTests`) drives the real Selenium strategy via the shared
  harness against the **pinned HTML fixture served locally** (headless driver; not the live site
  — CI-stable), asserting the normalized output. (2) **Library-parser integration test**
  (`PizzaHouseParserTests`) runs the AngleSharp strategy over its fixture asserting the **same
  normalized contract**. (3) Unit tests for each compliance gate (robots/rate-limit/first-party)
  and for the admin-protection persist gate (do-not-update / do-not-parse). (4) **Quarantine
  test:** a fixture menu containing **at least one unmappable dish name** plus several mappable
  ones asserts that the unmappable item **lands in the quarantine table** (`IParseQuarantineStore`
  / `0011_create_parse_quarantine.sql`), the parse **does not fail**, and the mappable items still
  persist as live `MenuItem`s (integration test over Testcontainers SQL, with a unit test on the
  `Normalizer` returning the mapped/unmapped split). Optionally document a separate,
  clearly-marked out-of-band **live-smoke** suite (manual/nightly) that respects robots/rate-limit
  — not run in CI.

### Step 10: Admin API host + EF Core admin CRUD (database-first) + protection flags

- **status:** [x] done
- **description:** Stand up the **separate admin-API host** (its own composition root, security
  isolation per the design) and the **Admin module** using **EF Core — the ONLY place EF is
  allowed**. Map EF **database-first** to the existing SQL-script-owned schema (Steps 4–5) with
  **EF migrations disabled** (no `Migrations` folder, no `EnsureCreated`/`Migrate`; configure the
  context so it never alters schema — assert via a startup guard). Provide admin CRUD use-cases:
  edit menu item / price / category, edit address (which raises the re-geocode flow), set the
  **per-MenuItem `DoNotParse`** flag and the **restaurant-level `DoNotUpdate`** flag (the
  admin > parser protection surface, invariant #3), and real-photo management for permission-
  granted (Verified) venues (toggles the Step 5 photo permission gate). The admin host validates
  the same OIDC tokens as the public host but **authorizes only admin roles/scopes** (admin-only
  policy via `BuildingBlocks.Auth`), with stricter network isolation expressed in config. EF
  entity configurations map to the exact table/column names the SQL scripts created.
- **files to modify:**
  - `src\Modules\Admin\WhereToEat.Admin.Application\` (`EditMenuItemCommand`,
    `SetMenuItemDoNotParseCommand`, `SetRestaurantDoNotUpdateCommand`, `EditAddressCommand`,
    `ManageRealPhotoCommand` + handlers)
  - `src\Modules\Admin\WhereToEat.Admin.Infrastructure\` (`AdminDbContext.cs`,
    `Configurations\*.cs` database-first mappings, `EfMigrationsDisabledGuard.cs`,
    `AddAdminModule.cs`) — **only project referencing `Microsoft.EntityFrameworkCore.SqlServer`**
  - `src\Hosts\WhereToEat.AdminApi\WhereToEat.AdminApi.csproj`, `...\AdminApi\Program.cs`,
    `Endpoints\AdminCatalogEndpoints.cs`, `Endpoints\ProtectionEndpoints.cs`,
    `Endpoints\PhotoEndpoints.cs`
  - `src\Shared\WhereToEat.BuildingBlocks.Auth\AdminAuthorization.cs` (admin-only policy)
  - admin host `appsettings*.json`
- **acceptance criteria:**
  - The admin host boots as a **distinct process** with its own port/config and serves only when
    an **admin-role** token is presented (non-admin authenticated user → 403; anonymous → 401).
    The `AdminAuthorization` admin-only policy is the **same policy** that Step 13 later exercises
    against the dev Keycloak realm (whose export defines the `admin`/`user` roles); this step tests
    it with a fake-authority test JWT, Step 13 confirms a real `admin` token satisfies it.
  - EF reads/writes admin rows over the **existing** schema with **no** EF migration artifacts;
    the startup guard fails fast if EF attempts schema changes.
  - Setting `DoNotUpdate` on a restaurant and `DoNotParse` on a menu item persists flags that the
    Step 9 parser persist gate observes (cross-checked by re-running a parse and confirming the
    flagged data is untouched).
  - Editing an address (`EditAddressCommand`) raises the Step 3 `AddressChanged` event, which the
    **Step 8** re-geocode handler consumes (the seam lives in Step 8, not here); the flow is
    observable (event/command emitted, geocode invoked).
  - The Step 2 **EF-containment** architecture test passes: EF is referenced **only** by
    `WhereToEat.Admin.Infrastructure`.
- **testing requirements:** Integration tests (`WebApplicationFactory` + Testcontainers SQL):
  admin CRUD round-trips over the script-created schema; the EF-migrations-disabled guard;
  admin-only authorization (admin token 200, user token 403, anonymous 401); and an
  **end-to-end protection test** — set `DoNotUpdate`/`DoNotParse`, run the Step 9 parse against a
  fixture, assert the protected restaurant/menu item is not overwritten. Unit tests for the admin
  command handlers.

### Step 11: Analytics ingest + anonymization-at-ingest + aggregation seam

- **status:** [x] done
- **description:** Implement the **Analytics ingest** path on the public host and the
  anonymization that happens **at ingest, before persistence** (so raw PII is never written —
  invariant #11 / privacy). Add an ingest endpoint accepting raw event payloads
  (impression / view / card_open / action / search / filter / rating_given / geo / session) and an
  `IAnonymizer` that applies the design's three rules: **retain** non-identifying dimensions
  (dish/category ids, sort mode + filter selections, result **position**, **coarse geohash** at
  reduced precision, **hour-truncated** timestamp); **hash** any user/session id with a
  **rotating salted hash** (salt rotated on a schedule via an `ISaltProvider`); **drop** precise
  lat/lng and all raw PII. Persist only the anonymized `AnalyticsEvent` (Step 5 domain) via an
  **append-optimized** Dapper writer (batch insert; JSON detail column for the variable bag).
  Provide the **aggregation seam**: a query/rollup interface that future B2B dashboards read
  (aggregates only) — implement a minimal rollup (e.g., impressions/CTR per restaurant per hour)
  to prove the path; the full dashboards are a non-goal. Publish an `AnalyticsEventRaised`
  contract on the bus (Step 13) so ingestion can later move to its own service.
- **files to modify:**
  - `src\Modules\Analytics\WhereToEat.Analytics.Application\` (`IngestEventCommand` + handler,
    `IAnonymizer.cs`, `ISaltProvider.cs`, `IAnalyticsRollupReader.cs`)
  - `src\Modules\Analytics\WhereToEat.Analytics.Infrastructure\` (`Anonymizer.cs`,
    `RotatingSaltProvider.cs`, `GeohashEncoder.cs`, `AppendOnlyAnalyticsWriter.cs` (Dapper batch),
    `AnalyticsRollupReader.cs`, `AddAnalyticsModule.cs`)
  - `...\PublicApi\Endpoints\AnalyticsEndpoints.cs` (`POST /analytics/events`)
  - `db\migrations\0012_create_analytics_rollups.sql` (rollup table)
- **acceptance criteria:**
  - An ingested event is persisted with **only** the anonymized shape: coarse geohash (reduced
    chars), hour-truncated time, dish/category ids, sort/filter, position, and a **hashed** user/
    session id — **no** precise lat/lng and **no** raw PII appear in the stored row.
  - Rotating the salt produces a **different** hash for the same user id across windows (not
    cross-window linkable), but a **stable** hash within a window (intra-window funnel math works).
  - The geohash precision is neighbourhood-grade (configurable char count), not point-grade.
  - The minimal rollup returns **aggregates only** (no per-user rows are queryable by venues).
  - Ingest is append-optimized (batch insert path exercised).
- **testing requirements:** Unit tests for `Anonymizer` proving the **retain/hash/drop** rules
  (assert dropped fields are absent, hashed fields are non-reversible, retained fields survive)
  and for `RotatingSaltProvider` (within-window stability, cross-window divergence). Integration
  test (Testcontainers SQL): ingest endpoint persists the anonymized row and the rollup reader
  returns the expected aggregate; a negative test asserts no precise-coordinate/PII column is ever
  populated.

### Step 12: Worker host + Quartz.NET clustered scheduler + the four background jobs

- **status:** [x] done
- **description:** Create the **worker host** (its own composition root, separately deployable so
  parsing never competes with request-serving) and wire **Quartz.NET with a clustered, DB-backed
  job store** so that with multiple worker replicas a given scheduled run fires on **exactly one**
  replica. Implement the four jobs from the design: (1) **weekly parse** — invokes the Step 9
  `RunParseCommand` for each first-party source (compliance + admin-protected persist); (2)
  **geocode refresh** — consumes `AddressChanged`/queued geocode work and calls the Step 8
  geocoder; (3) **nightly price-median refresh** — recomputes the per-area/per-category medians
  (city-wide fallback below threshold N) from current `MenuItem` prices and **writes them into the
  price-median table created in Step 7** — the same table Step 7's `DbPriceMedianProvider` reads
  **directly from the DB (DB-only)**. This job does **not** require Redis: Step 13 later adds Redis
  as a *transparent caching layer in front of `IPriceMedianProvider`*, so the read path is
  cache-then-DB without this job changing. Step 12 therefore writes the DB table only; Step 7
  remains buildable/testable on this DB table without the worker or Redis. (4) **rating
  recomputation** — refreshes the materialized Bayesian-smoothed rating aggregates from raw
  ratings (the same aggregate the Step 7 candidate source joins as the smoothed-rating DTO field).
  Add the Quartz cluster tables to the SQL migrations. Jobs are idempotent and observable
  (correlation ids, structured logs from Step 13).
- **files to modify:**
  - `src\Hosts\WhereToEat.Worker\WhereToEat.Worker.csproj` (Quartz, Quartz.Extensions.Hosting)
  - `...\Worker\Program.cs` (composition root + Quartz clustered config),
    `Jobs\WeeklyParseJob.cs`, `Jobs\GeocodeRefreshJob.cs`, `Jobs\NightlyPriceMedianJob.cs`,
    `Jobs\RatingRecomputeJob.cs`, `Scheduling\JobSchedule.cs`
  - `src\Modules\Recommendation\WhereToEat.Recommendation.Infrastructure\Medians\PriceMedianCalculator.cs`
    (+ the writer that populates the Step 7 price-median table the DB-only provider reads)
  - `db\migrations\0013_quartz_cluster_tables.sql` (the price-median **table** is created in Step 7's
    `0010_create_price_medians.sql`; this step only writes/refreshes its rows)
- **acceptance criteria:**
  - The worker boots, registers the four jobs on their schedules, and uses the **clustered**
    Quartz store (cluster tables present; misfire/recovery configured).
  - With two worker instances pointed at the same store, a scheduled trigger executes on **one**
    instance only (verified by a job that records its execution count).
  - The nightly median job writes per-area/per-category medians **into the Step 7 price-median
    table** and the **city-wide fallback** is used when an area's sample size is below N; Step 7's
    `DbPriceMedianProvider`/`f_price` then reads a ready value from that DB table (no Redis
    required for this job — Redis is added transparently in Step 13).
  - The rating-recompute job materializes the Step 5 Bayesian-smoothed aggregate; values match the
    pure-domain formula.
  - The weekly-parse job honors admin protection (delegates to Step 9, which already gates it).
- **Known deferral — RESOLVED in Step 13:** the parse-persist/geocode seams `ICatalogMenuWriter`,
  `ICatalogDishResolver` and `IRestaurantGeocoder` now have production adapters
  (`Catalog.Infrastructure\Parsing\{DapperCatalogMenuWriter, DapperCatalogDishResolver}`,
  `Geo.Infrastructure\RestaurantGeocoderAdapter`) registered in `AddCatalogPersistence`/`AddGeoModule`
  via `TryAdd*`. The Worker's `WeeklyParseJob` full dependency graph therefore resolves at startup
  (no first-fire throw), proven by the Worker DI-resolution test in `WhereToEat.Worker.Integration`.
- **testing requirements:** Integration tests (Testcontainers SQL for the Quartz cluster tables +
  the Step 7 price-median table): the **single-fire-under-clustering** behavior (two scheduler
  instances, one execution), the median calculation incl. the city-wide fallback below threshold N
  (assert the rows written are exactly what the Step 7 DB-only provider would read back), and the
  rating-recompute matching the domain formula. Unit tests for `PriceMedianCalculator` (per-area
  vs. fallback selection) and `JobSchedule` cron wiring.

### Step 13: Cross-cutting — Redis cache, MassTransit bus, logging/observability, Docker + compose, scaling knobs

- **status:** [x] done
- **description:** Land the cross-cutting infrastructure that the design commits to, behind clean
  abstractions so backends stay swappable and services stay stateless. (1) **Redis cache** —
  `ICacheService` abstraction in `BuildingBlocks.Caching` with a `StackExchange.Redis`
  implementation; back the hot read paths (taxonomy lists, the `f_price` medians, rating
  aggregates) and the parser rate-limit counters; **never** cache Google ratings/coords. For the
  `f_price` medians specifically, add Redis as a **transparent caching layer in front of Step 7's
  `IPriceMedianProvider`** — a caching decorator that wraps the existing `DbPriceMedianProvider`
  (cache-hit → return; miss → read the Step 7 DB median table → populate cache). This is a
  **drop-in decorator registered in DI**; it does **not** change the `IPriceMedianProvider`
  contract, its callers in Step 7, or the Step 12 writer — so Step 7/12 stay DB-only and correct
  whether or not this cache is present. (2)
  **MassTransit bus** — `BuildingBlocks.Messaging` configuring MassTransit with the **in-process
  transport now** and **RabbitMQ-ready** config (transport chosen by configuration), carrying the
  `Contracts` integration events (`MenuUpdated`, `RatingGiven`, `AnalyticsEventRaised`); add a
  couple of consumers (e.g., geocode-on-`AddressChanged`, invalidate-cache-on-`MenuUpdated`) that
  depend **only on Contracts** (Step 2's consumer-boundary fitness test must stay green). (3)
  **Observability** — structured logging (Serilog or `Microsoft.Extensions.Logging` + a JSON
  sink), **correlation IDs** propagated across API↔bus↔worker, and **OpenTelemetry** traces/metrics
  wired in each host's composition root. (4) **Containerization** — a `Dockerfile` per host
  (public API, admin API, worker) and a `docker-compose.yml` for local dev bringing up **SQL
  Server, Redis, RabbitMQ, and an OIDC dev provider** (e.g., Keycloak) plus the three hosts;
  expose **config-driven replica/scaling knobs** (env-driven replica counts / resource limits) and
  a `.env.example`. The migration runner runs on host startup (or a one-shot compose service).
- **files to modify:**
  - `src\Shared\WhereToEat.BuildingBlocks.Caching\` (`ICacheService.cs`, `RedisCacheService.cs`,
    `AddRedisCache.cs`)
  - `src\Shared\WhereToEat.BuildingBlocks.Messaging\` (`AddMessaging.cs` with in-process vs.
    RabbitMQ switch, consumers `GeocodeOnAddressChangedConsumer.cs`,
    `InvalidateCacheOnMenuUpdatedConsumer.cs`)
  - `src\Shared\WhereToEat.BuildingBlocks.Observability\` (`AddObservability.cs`, correlation-id
    middleware, OpenTelemetry wiring)
  - Wire `AddRedisCache` / `AddMessaging` / `AddObservability` into all three hosts' `Program.cs`
  - `deploy\Dockerfile.PublicApi`, `deploy\Dockerfile.AdminApi`, `deploy\Dockerfile.Worker`
  - `deploy\docker-compose.yml`, `deploy\.env.example`, `deploy\keycloak\realm-export.json`
    (dev IdP realm: defines both an **`admin`** and a **`user`** role/scope, a client for the API
    hosts, and at least one admin test user, so the admin-only policy from Steps 10/14 can be
    exercised end-to-end against the dev IdP)
  - `src\Modules\Recommendation\WhereToEat.Recommendation.Infrastructure\Medians\CachingPriceMedianProvider.cs`
    (Redis decorator wrapping the Step 7 `DbPriceMedianProvider`; same `IPriceMedianProvider`
    contract)
- **acceptance criteria:**
  - `ICacheService` get/set/remove works against Redis; the medians/taxonomy/rating reads are
    cache-backed with a miss→DB→populate path; **no** Google rating/coordinate value is ever
    cached (verified by inspection + a guard test).
  - The **`CachingPriceMedianProvider` decorator** is transparent: with it registered, a cache miss
    reads the Step 7 DB median table and populates the cache, a hit skips the DB, and the
    `IPriceMedianProvider` contract/return values are identical to the DB-only provider — Step 7's
    recommendation results are unchanged whether the decorator is present or not.
  - The Keycloak **dev realm export defines `admin` and `user` roles/scopes**, and a **test JWT
    carrying the `admin` role/scope satisfies the admin-only authorization policy** used by Steps 10
    and 14 (a `user`-only token does not; an anonymous request is rejected) — verified against the
    compose Keycloak (or an equivalent fake-authority test).
  - Publishing `MenuUpdated` invalidates the relevant cache entry via the consumer; the geocode
    consumer reacts to `AddressChanged` — both consumers reference **only** `Contracts` (Step 2
    fitness test green).
  - Switching the messaging transport from in-process to RabbitMQ is a **config** change (the
    RabbitMQ path connects to the compose broker).
  - A request's **correlation ID** appears in logs across the public API → bus → worker; traces/
    metrics are emitted.
  - `docker-compose up` brings up SQL Server, Redis, RabbitMQ, Keycloak, and the three hosts; the
    hosts pass health checks; replica/scaling knobs are env-driven (documented in `.env.example`).
- **testing requirements:** Integration tests: `RedisCacheService` against a Redis container
  (Testcontainers) for get/set/remove/expiry; a test for the **`CachingPriceMedianProvider`
  decorator** proving miss→DB→populate then hit→no-DB and identical return values vs. the DB-only
  provider (so Step 7 behavior is unchanged with caching on/off); a MassTransit **in-memory test
  harness** asserting the consumers handle `MenuUpdated`/`AddressChanged` and reference only
  Contracts; a correlation-id propagation test across a publish→consume hop; and a **Keycloak
  realm/authorization test** asserting the dev realm export contains `admin` and `user`
  roles/scopes and that a **test JWT with `admin` passes the admin-only policy** (Steps 10/14)
  while a `user`-only token gets 403 and anonymous gets 401. A **manual/CI smoke**:
  `docker-compose up` then hit each host's health endpoint (document the commands). Re-run the
  Step 2 architecture tests (consumer-to-contract rule) — must stay green.

### Step 14: Monetization module skeleton — Verified/ads behind a payment seam (future-facing)

- **status:** [x] done
- **description:** Flesh out the **Monetization module** skeleton (future-facing; no real billing —
  a non-goal). Build on the Step 5 domain types: `VerifiedStatus` + `SubscriptionTier`
  (Basic/Pro) and `Promotion`/`AdPlacement`. Define application use-cases to grant/revoke a
  venue's Verified status and to create a **labeled** ad placement, with **payment kept strictly
  behind an `IPaymentGateway` seam** (a no-op/dev implementation only — no provider integration).
  Reinforce the invariants in code: an `AdPlacement` is **always labeled** and is a **separate,
  marked slot** that the recommendation pipeline (Step 7) **never** uses as a rank modifier —
  organic ranking is not for sale (invariant #10); and Verified unlocks the real-photo permission
  gate (Step 5) but **does not** affect organic score or the always-free contact links (§5.8).
  Expose admin-side endpoints (on the admin host) to manage Verified/ads. Keep the surface minimal:
  the goal is a correct, extensible seam that future billing plugs into, not a complete product.
- **files to modify:**
  - `src\Modules\Monetization\WhereToEat.Monetization.Application\` (`GrantVerifiedCommand`,
    `CreateAdPlacementCommand`, `IPaymentGateway.cs`)
  - `src\Modules\Monetization\WhereToEat.Monetization.Infrastructure\` (`NoOpPaymentGateway.cs`,
    Dapper repos for verified/ads, `AddMonetizationModule.cs`)
  - `db\migrations\0014_monetization_indexes.sql` (any indexes/links not in Step 5's
    `0008`)
  - `src\Hosts\WhereToEat.AdminApi\Endpoints\MonetizationEndpoints.cs`
  - A guard test/assertion confirming the recommendation pipeline has **no** dependency on
    Monetization (organic ranking unsellable).
- **acceptance criteria:**
  - Granting Verified flips the Step 5 photo-permission gate (real photos become allowable) and
    sets the tier — with **no** effect on organic ranking or contact-link visibility.
  - Creating an `AdPlacement` produces an **always-labeled** placement; there is **no** code path
    by which an ad influences the Step 7 organic score (verified by an architecture/guard test:
    Recommendation does not reference Monetization).
  - `IPaymentGateway` is the only payment touch-point and resolves to a **no-op** dev
    implementation (no external provider).
  - Admin endpoints manage Verified/ads and are admin-authorized by the **same Step 10
    `AdminAuthorization` admin-only policy** (the policy whose real `admin`-token behaviour the
    Step 13 Keycloak realm/authorization test verifies).
- **testing requirements:** Unit tests: grant/revoke Verified toggles the photo gate and tier
  without touching ranking; `AdPlacement` is always labeled; the no-op payment gateway is the sole
  payment path. Architecture/guard test: `WhereToEat.Recommendation.*` has **no** reference to
  `WhereToEat.Monetization.*` (organic ranking is not for sale). Integration test for the admin
  Verified/ads endpoints (admin-authorized).

---

## Build-order summary (dependency spine)

1. Step 1 — solution + SharedKernel + Contracts (foundation everything imports).
2. Step 2 — NetArchTest fitness tests (guards every later step).
3. Step 3 — Catalog domain core (taxonomy ER + invariants).
4. Step 4 — SQL scripts + DbUp runner + Dapper data layer + spatial (containerized SQL test).
5. Step 5 — remaining domains (Ratings/Bayesian, Users, Photos, Verified, Ads, Protection, Analytics).
6. Step 6 — public API host + Catalog + `Catalog.Search` + OIDC + composition root (Map endpoint
   is a **DB-only stub**: stored coordinates / Place ID / deep-link, **no** geocoder; the
   AddressChanged→re-geocode seam is deferred to Step 8).
7. Step 7 — recommendation engine (pluggable strategies; explicit-sort-dominates). **Creates** the
   price-median table (`0010`) and reads it via a **DB-only** `IPriceMedianProvider`; rating
   crosses module lines only as a **candidate-DTO field** (no `Ratings.Domain` reference).
8. Step 8 — Geo module (Nominatim port + adapter + distance) — **owns** the AddressChanged→
   re-geocode seam end-to-end (first appearance of `IGeocoder`).
9. Step 9 — parsing module + compliance + admin-protection + Selenium/library + e2e harness;
   unmappable dishes go to a **quarantine table** (`0011`), parse never fails.
10. Step 10 — admin API host + EF Core CRUD (database-first) + protection flags (`AddressChanged`
    raised here, consumed by the Step 8 seam).
11. Step 11 — analytics ingest + anonymization-at-ingest + aggregation.
12. Step 12 — worker host + Quartz clustered + four jobs; the nightly job **populates** the Step 7
    price-median table (DB-only; no Redis dependency).
13. Step 13 — Redis + MassTransit + observability + Docker/compose + scaling. Redis wraps the Step 7
    `IPriceMedianProvider` as a **transparent caching decorator**; the dev Keycloak realm export
    defines `admin`/`user` roles and an `admin` test JWT satisfies the Step 10/14 admin policy.
14. Step 14 — monetization skeleton (Verified/ads behind a payment seam).

Each step compiles, its tests pass, and the Step 2 architecture-fitness tests stay green before
the next step starts. **Migration numbering is global and monotonic with build order** (DbUp
applies by number): `0001`–`0009` (Steps 4–5), `0010` price-medians (Step 7), `0011` parse
quarantine (Step 9), `0012` analytics rollups (Step 11), `0013` Quartz cluster (Step 12), `0014`
monetization indexes (Step 14).
