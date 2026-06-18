# Developer notes — backend-foundation

## Environment
- Bash tool runs under **bash**, not PowerShell. Use forward-slash absolute paths
  (`cd "D:/Projects/VS_Projects/DePoisty"`); `cd /d ...` (cmd syntax) fails.
- dotnet CLI output is **localized to Russian** here: `Предупреждений` = warnings,
  `Ошибок` = errors, `пройдено/не пройдено/пропущено` = passed/failed/skipped.
- SDK pinned to 8.0.417 via `global.json` (rollForward latestFeature); active SDK matches.

## Central build config (Step 1)
- `Directory.Build.props` sets net8.0, nullable, implicit usings, **TreatWarningsAsErrors**,
  analyzers latest-recommended. Applies to every project under repo root.
- `Directory.Packages.props` = central package management (versions pinned once). **No** Version
  attribute belongs on `<PackageReference>` in csproj.
- FluentAssertions intentionally pinned to **7.2.2** (8.x is a paid license).
- Test projects relax CA1707/CA1034/CA2007 via `tests/.editorconfig` (so underscore test names
  compile under warnings-as-errors). This does NOT apply to `src/`.

## Step 1 state (finalized, awaiting QA)
- SharedKernel + Contracts complete; both have **zero** package refs (incl. transitive) and
  reference nothing else in the solution — the innermost stable layers.
- `Result<T>.Value` throws InvalidOperationException on failure; `Money` rejects negative +
  mismatched-currency arithmetic (throws on arithmetic, returns Result on construction);
  `GeoPoint` validates [-90,90]/[-180,180] and rejects non-finite; `Haversine.DistanceKm`
  short-circuits identity to exact 0.
- Added empty solution folders `01-Modules` / `02-Hosts` to `src/WhereToEat.sln` (the step lists
  all four: 00-Shared, 01-Modules, 02-Hosts, 03-Tests); they hold no projects until later steps.
- Build: 0 warnings / 0 errors. Tests: 50 passed / 0 failed / 0 skipped.

## Step 2 state (finalized after dev-reviewer revision, awaiting QA)
- `tests/Architecture/WhereToEat.ArchitectureTests` (NetArchTest.Rules **1.3.2**, pinned centrally).
  Registered in `src/WhereToEat.sln` under `03-Tests` (GUID `C387CF23-...`).
- **How a module joins the guards:** add a `ProjectReference` to it in
  `WhereToEat.ArchitectureTests.csproj` so its assembly lands in the test output; `AssemblyLoader`
  then discovers every `WhereToEat.*` (non-`*Tests`) dll there at runtime. Rules grow automatically.
- **ENGINE SPLIT (dev-reviewer Finding 1 — NetArchTest was inert, now genuinely drives 4 rules):**
  rules (a)/(b)/(c)/(d) use **NetArchTest fluent API** — `Types.InAssembly[ies](...)` +
  `.That().DoNotResideInNamespace(...)` / `.ShouldNot().HaveDependencyOn[Any](...)` + `.GetResult()`,
  bridged to FluentAssertions by `NetArchTestExtensions.ShouldHold(result, because)` (names failing
  types). Forbidden namespace sets are computed from the convention-discovered assemblies
  (`AssemblyLoader.NamespacesWithLayerSuffix` / `HostNamespaces`). Rule (e) consumer-structural scan
  + the `ModuleGrouping` helper **stay on System.Reflection** (member-signature scan NetArchTest
  can't express). NetArchTest matches namespaces/deps by **prefix**, but forbidden lists hold full
  assembly names (e.g. `WhereToEat.Recommendation.Domain`), so no prefix-bleed false positives.
- 5 rule classes: `LayerDependencyTests` (a Domain→only-SharedKernel, b Application→no Infra/Host),
  `EfCoreContainmentTests` (c EF only in `WhereToEat.Admin.Infrastructure`), `ModuleBoundaryTests`
  (d cross-module only via Contracts), `MessageConsumerBoundaryTests` (e `IConsumer<>` types touch
  only Contracts across module lines). Plus `ModuleGroupingTests` pinning `Catalog.Search.*`→Catalog.
- **`ModuleGrouping` helper:** module = first ns segment after `WhereToEat`; special-case
  `Catalog.Search.*`→`Catalog`; shared families (`SharedKernel`/`Contracts`/`BuildingBlocks.*`) and
  hosts (`*Api`/`*Host`/`*Worker`) are NOT modules (return null). `AreDifferentModules` is the
  cross-module predicate rule (e) uses; the (d) rule groups `ModuleAssemblies` by `ResolveModule`
  and forbids other modules' full assembly names.
- **IMPORTANT (negative-check / future EF-in-Admin semantics):** NetArchTest analyses **IL type
  usage** (Mono.Cecil). A *bare/unused* `PackageReference`, or compile-time-constant use like
  `nameof(DbContext)`, leaves no IL type reference → invisible. Rules fire only when a forbidden type
  is **actually used in a signature/body** (the case that matters). Rule (e)'s reflection scan
  likewise only sees types in member signatures.
- **Negative check performed + reverted (proves NetArchTest rule bites):** temp EF pkg ref +
  `DbContextId?` static property on `SharedKernel.TempEfViolation` made `EfCoreContainmentTests`
  fail, naming `WhereToEat.SharedKernel.TempEfViolation`; fully reverted (clean rebuild leaves 0 EF
  dlls). Procedure documented in the project's `README.md` (the "non-skippable in CI" note lives
  there too — no CI yaml exists yet; CI is a later step).
- **dev-reviewer Findings 2–4 fixed:** (2) added `BindingFlags.Static` to the consumer member scan
  in `MessageConsumerBoundaryTests`; (3) renamed `referencingModule`/`consumerModule` →
  `*AssemblyName` (they held assembly names, not modules); (4) corrected the NetArchTest comment in
  `Directory.Packages.props` + csproj + README to state the NetArchTest/reflection split accurately.
- Build: 0 warnings / 0 errors. Tests this step: Architecture **33** passed; full solution
  Contracts 5 + Architecture 33 + SharedKernel 71 = **109** passed / 0 failed / 0 skipped.

## Step 4 state (finalized, awaiting QA)
- **Migration scripts** at repo-root `db/migrations/*.sql` (schema-of-record): `0000_create_schema`
  (bootstraps `catalog` schema — CREATE SCHEMA must be first in its batch → single statement, no
  GO), `0001` taxonomy (Category/Dish), `0002` Restaurant+Address+ContactLink, `0003` MenuItem.
- Scripts are **embedded** into `WhereToEat.BuildingBlocks.Migrations` via a csproj glob with
  `LogicalName` prefix `WhereToEat.Migrations.Scripts.`, so DbUp applies them in numeric order
  regardless of CWD. `MigrationRunner.Run(conn)` is idempotent (DbUp journal); returns
  `MigrationResult` (no DbUp types leak). Console tool `tools/WhereToEat.MigrationRunner`.
- **Geography (invariant #7):** `catalog.Restaurant.Location GEOGRAPHY` SRID 4326 + spatial index
  `SPATIAL_Restaurant_Location` (GEOGRAPHY_AUTO_GRID). **No raw Google lat/lng column.** Read/write
  via WKT (`geography::STGeomFromText(@Wkt,4326)` / `.Lat`/`.Long`) — **no native SqlServer.Types
  interop**. WKT is **longitude-first**.
- **Radius pre-filter** lives in a separate infra read-model `ICatalogSpatialReader`
  (DapperCatalogSpatialReader), NOT on the domain `ICatalogRepository` port (avoids a domain
  change): SQL `Location.STDistance(@origin) <= @RadiusMeters` narrows; **Haversine orders in code**.
- `DapperCatalogRepository` implements the Step 3 port; `UpdateRestaurantAsync` = delete-all-children
  + re-insert (catalog writes are low-frequency). `RowMapper` rehydrates through domain factories
  then `ClearDomainEvents()` so reads don't re-raise AddressChanged/MenuItemUpserted.
- **Packages added** (Directory.Packages.props): Dapper 2.1.66, Microsoft.Data.SqlClient 5.2.2,
  dbup-sqlserver 6.0.0, DI/Config/Hosting Abstractions 8.x, Testcontainers.MsSql 3.10.0.
- **Solution:** new convention folder `04-Tools` (MigrationRunner); shared blocks under `00-Shared`,
  Catalog.Infrastructure under `01-Modules`, integration test under `03-Tests`. `dotnet sln add`
  created junk duplicate folders → the .sln was hand-rewritten to the existing convention.
- **Catalog.Infrastructure wired into ArchitectureTests** csproj; all 33 rules green (Dapper-only,
  EF-containment still vacuously green). Arch rules see BuildingBlocks.Persistence as a shared family.
- Test outcomes: build 0 warn / 0 err; arch **33/33**; **Testcontainers SQL integration 5/5 green**
  (`mcr.microsoft.com/mssql/server:2022-latest`; first image pull ~10 min, exec ~1s after).
  Integration asserts: scripts applied + idempotent re-run no-op; Location column type=`geography`
  with a spatial index and no raw lat/lng column; aggregate round-trip (Money/PlaceId/coords);
  radius pre-filter returns only in-radius candidates ordered by Haversine.

## Step 5 state (finalized, awaiting QA) — crash recovery completed
- **Ratings:** prior session built BayesianSmoothing/Rating/RatingSmoothingOptions/identifiers. This
  session added `RatingAggregate` (cumulative Sum+Count, ApplyNewScore/ApplyScoreRevision, SmoothedValue
  delegates to BayesianSmoothing), the `IRatingAggregateRepository` port, and `Ratings.Infrastructure`
  (DapperRatingAggregateRepository — keyed SELECT hot read + MERGE upsert for the Step 12 recompute job).
- **Users:** minimal PII-free `User` aggregate (internal UserId, opaque ExternalSubject=iss+sub VO, UserRole
  enum). Baseline User role always present; Admin grant/revoke; baseline cannot be revoked. NO email/name.
- **Photos:** `catalog.Photo` aggregate (Catalog.Domain/Photos) — Generic (our content, always displayable,
  default) vs RealWithPermission; `CanBeDisplayed()` = IsGeneric || PermissionGranted (invariant #8 gate);
  RevokePermission flips real→hidden, no-op on generic.
- **Monetization:** VerifiedStatus(+SubscriptionTier None/Basic/Pro; CanManageRealPhotos=IsVerified),
  AdPlacement, Promotion. **Invariant #10 guard:** IsLabeledAd is a const-true INSTANCE prop (CA1822
  suppressed w/ justification — must stay instance so the reflection test reads it on the public surface);
  a Theory test scans AdPlacement/Promotion public props+fields for tokens
  (rank/boost/weight/priority/score/relevance/organic/position/sortorder) and asserts NONE exist.
- **Admin:** `RestaurantProtection` (admin/Protection) — DoNotUpdate flag + ShouldParserSkip()
  (invariant #3, restaurant-level; complements Step 3 per-MenuItem DoNotParse). Admin.Domain refs only
  SharedKernel (EF is a LATER Admin.Infrastructure concern, not here).
- **Analytics (domain SHAPE only — ingest is Step 11):** AnalyticsEvent + EventKind + anonymization VOs:
  Geohash (base32, MaxPrecision=6 cap so never point-precise), HashedActorId (64-char hex, rejects raw),
  HourBucket (FromInstant truncates to UTC hour — only coarsening ctor), EventDimensions (retain). The
  aggregate CANNOT represent dropped data (no raw-id/precise-coord/minute-time property). Plus
  `Analytics.Infrastructure` append-optimized writer (AppendOnlyAnalyticsWriter, Dapper batch, JSON
  dimensions) + internal AnalyticsEventReader (InternalsVisibleTo the Integration test) for round-trip.
- **Migrations 0004-0009** (all idempotent atop 0000-0003; each `IF NOT EXISTS CREATE SCHEMA` then GO):
  0004 ratings (Rating + RatingAggregate; smoothed value NOT stored — pure math over ScoreSum/ScoreCount),
  0005 users (User + UserRole; (Issuer,Subject) unique; no PII cols), 0006 catalog.Photo (CHECK: generic
  has no permission), 0007 admin.RestaurantProtection, 0008 monetization (VerifiedStatus/AdPlacement/
  Promotion — NO rank/score column by design), 0009 analytics.AnalyticsEvent (no raw-id/precise-coord col;
  ActorHash CHAR(64) NULL, CoarseGeohash VARCHAR(12) NULL, hour-truncated time, JSON dimensions).
- **Module isolation:** every cross-module ref uses an opaque Guid-wrapper (Ratings.RestaurantRef/UserRef,
  Monetization.VenueRef, Admin.VenueRef) NOT another module's id type — module-isolation rule (d) is now
  NON-VACUOUS with 6 modules present and stays GREEN (33/33 arch tests).
- **sln wiring:** hand-edited src/WhereToEat.sln (NOT `dotnet sln add` — it makes junk folders). 14 new
  projects: 7 src (Ratings.Domain[prior]/Infra, Users.Domain, Monetization.Domain, Admin.Domain,
  Analytics.Domain/Infra) under 01-Modules; 5 unit + 2 integration under 03-Tests. ALL new Domain+Infra
  assemblies added as ProjectReference to ArchitectureTests.csproj.
- **2 integration test projects** (Testcontainers SQL, mssql:2022-latest) each with their own SqlServerFixture
  (copy of Catalog's): Ratings (aggregate read/upsert round-trip) + Analytics (append round-trip + schema
  has no raw-id/coord col). Both pull the FULL 0000-0009 migration set, proving idempotent apply.
- Build: 0 warn / 0 err. Tests: SharedKernel 71 + Contracts 5 + Catalog.Domain 28 + Ratings.Domain 29 +
  Users 8 + Monetization 12 + Admin 4 + Analytics.Domain 11 = 168 unit; Architecture 33; Integration
  Catalog 7 + Ratings 4 + Analytics 5 = 16. ALL green, 0 skipped.

## Step 6 state (finalized, awaiting QA)
- New projects: `WhereToEat.Catalog.Application`, `WhereToEat.Catalog.Search.Application`,
  `WhereToEat.BuildingBlocks.Auth`, host `WhereToEat.PublicApi` (SDK.Web, Minimal API);
  tests `WhereToEat.PublicApi.Integration` (WebApplicationFactory + Testcontainers SQL),
  `WhereToEat.Catalog.Application.UnitTests` (NSubstitute). All in sln + nested in virtual folders.
  `dotnet sln add` auto-creates DUPLICATE physical folders (Modules/Catalog/Shared/Hosts) — had to
  delete those folder nodes and remap NestedProjects to the existing 00-Shared/01-Modules/02-Hosts/
  03-Tests GUIDs by hand.
- **Read-side queries added to `ICatalogRepository`** (Domain port): ListCategoriesAsync,
  ListDishesByCategoryAsync, SearchCategoriesByPrefixAsync, SearchDishesByPrefixAsync. Implemented in
  DapperCatalogRepository + CatalogSql (anchored `LIKE @Prefix + N'%' ESCAPE N'\'`; LIKE wildcards
  escaped in EscapeLikePrefix — deterministic prefix, NOT NLP, invariant #1).
- Handlers are plain classes (no MediatR). `AddCatalogModule(connStr)` lives in Catalog.Infrastructure
  (registers persistence + the 4 query handlers); `AddCatalogSearchModule()` in Search.Application.
- **Auth seam (BuildingBlocks.Auth):** `IIdentityContext`/`HttpContextIdentityContext`,
  `IClaimsToRoleMapper`/`RoleClaimMapper` (maps admin/user from role/roles/ClaimTypes.Role),
  `UserRole`, `OidcOptions`, `AddOidcResourceServerAuth(config, configureJwtBearer?)` + policies
  RequireUser/RequireAdmin. Validates external IdP tokens only (no issuance). FrameworkReference
  Microsoft.AspNetCore.App + JwtBearer pkg.
- **Auth test seam:** Program reads env `WHERETOEAT_TEST_SIGNING_KEY` (dev/test only) → accepts
  HS256-signed test JWTs without a live IdP. Fixture sets the env var + UseSetting overrides for
  conn string / blank authority. `/me` is the protected probe (401 anon, 200 valid token); reads
  stay anonymous.
- **Map endpoint = DB-only stub:** GetRestaurantMapPayloadQuery → MapPayloadDto built ONLY from
  stored Coordinates (lat/lng) + PlaceId + MapsDeepLink; HasMapData=false (NoMapData) when no coords;
  null → 404. No geocoder dependency anywhere (verified: no Geo*.dll in host output). No Google
  rating field exists on the payload.
- **Arch tests:** added Catalog.Application + Catalog.Search.Application ProjectReferences to
  WhereToEat.ArchitectureTests.csproj. ModuleGrouping already maps Catalog.Search.* → Catalog. 33
  arch tests green (Application-no-Infrastructure + module-isolation hold).
- WebApplicationFactory gotcha: `ConfigureAppConfiguration.AddInMemoryCollection` did NOT override
  the conn string (provider ordering) — use `builder.UseSetting(...)` instead.
- Results: build 0 warn/0 err; 14 PublicApi integration + 10 Catalog.Application unit pass; full
  suite 243 tests, 0 failed, 0 skipped.

## Step 7 — Recommendation module (in-progress, awaiting QA)
- Projects: `WhereToEat.Recommendation.{Domain,Application,Infrastructure}` under
  `src/Modules/Recommendation/`. Registered in sln + arch-test csproj.
- **Migration `0010_create_price_medians.sql`** CREATES `recommendation.PriceMedian`
  (AreaKey VARCHAR(12), CategoryId/DishId exactly-one filtered-unique, MedianAmount, Currency,
  SampleSize). City-wide fallback row = `AreaKey '*'`. **Step 12 nightly job POPULATES it** (table
  already exists). Area-key grid = floor(lat|lng / 0.05deg) -> "latCell:lngCell"
  (`Infrastructure/Medians/AreaKey.cs`); Step 12 writer MUST use the same grid.
- **Cross-module boundary:** Recommendation has ZERO ref to Ratings.*. Smoothed rating crosses ONLY
  as `RecommendationCandidate.SmoothedRating` (Application DTO). Dapper candidate source LEFT JOINs
  `ratings.RatingAggregate` (ScoreSum/ScoreCount) and applies Bayesian inline in
  `Infrastructure/Candidates/SmoothedRatingMath.cs` (RatingSmoothingSettings: C=10, m=4.0 defaults).
  Generic `ModuleBoundaryTests` guards this — no new arch test needed.
- **IPriceMedianProvider DB-only** (`DbPriceMedianProvider`). Step 13 wraps the SAME port with a
  Redis decorator — do not change the port/callers.
- **Pluggability:** match/sort/filter keyed in `AddRecommendationModule`; `KeyedStrategyResolver`
  indexes by `.Key` (OrdinalIgnoreCase). New mode = add to DI collection only. Composite weights in
  `Infrastructure/DependencyInjection/RecommendationModes.cs`.
- **Invariant #5:** `ExplicitSortStrategy` primary key + `.ThenByDescending(Coverage)` tie-breaker +
  RestaurantId. `CompositeScoreStrategy` = weighted sum of `IScoringFunction`s.
- **Endpoint** `POST /recommend` (`PublicApi/Endpoints/RecommendEndpoints.cs`) uses
  `RecommendRequestBody.TryToContract()` (returns `Result<RecommendationRequest>`) because Contracts
  `SelectedItem` has a private ctor (cannot bind). 400 for: empty selection, unknown
  match|sort|filter, item with neither/both ids (no 500 from the factory), out-of-range UserGeo
  (validated via `GeoPoint.Create`, no silent distance drop).
- **Single Bayesian smoothing (Step 7 rev):** the one authoritative formula + canonical constants
  (`DefaultPriorWeight=10`, `DefaultGlobalMean=3.5`) live in
  `SharedKernel/Ratings/BayesianRatingSmoothing.cs`. `Ratings.Domain.BayesianSmoothing` and
  `RatingSmoothingOptions.Default` delegate to it; `Recommendation.Infrastructure`'s
  `SmoothedRatingMath`/`RatingSmoothingSettings` defaults source from it. Both modules reference
  SharedKernel only — no cross-module ref (arch test green). Engine now smooths with m=3.5 (was 4.0).
- `RecommendationModeOptions` no longer carries the four composite weights (dead/misleading) — weights
  live solely in the DI weight map (`RecommendationModes.cs`) read by `CompositeScoreStrategy`.
- `ISqlConnectionFactory` registered via `TryAddSingleton` in BOTH `AddRecommendationModule` and
  `CatalogInfrastructureServiceCollectionExtensions` (no double registration when host wires both).
- Integration test independence: `RecommendSeeder` seeds rating aggregate AND median row directly
  (no Step 12 job). Composite price-quality test margin is real but modest (~0.678 vs 0.674) —
  driven by the seeded 4.76 vs 3.05 smoothed ratings.
- Results: build 0 warn/0 err; 39 new unit + 4 new integration; full suite 0 failed/0 skipped
  (Recommendation.Domain.UnitTests 39, arch 33, PublicApi.Integration 19).

## Step 8 — Geo module (Nominatim geocoder + AddressChanged→re-geocode seam)
- **Cross-module boundary** (key reuse pattern for later steps): Geo must NOT reference Catalog
  internals (module-isolation rule d). Mirrored Step 7's DTO-seam approach:
  - `Contracts/Geo/ICatalogCoordinateWriter.cs` — read geocoding address / write OSM coords (keyed
    by `Guid`). `Contracts/Geo/GeoCoordinatesDto.cs` — plain lat/lng (Contracts references nothing,
    so it can't use `GeoPoint`). `Contracts/IntegrationEvents/RestaurantAddressChanged.cs` —
    Contracts projection of the Catalog domain `AddressChanged` (handler depends only on Contracts).
  - Adapter `CatalogCoordinateWriter` lives in **Catalog.Infrastructure** (over `ICatalogRepository`),
    registered in `CatalogInfrastructureServiceCollectionExtensions`. Catalog.Infrastructure now also
    references `WhereToEat.Contracts`. Geo.Application/Infrastructure reference only SharedKernel +
    Contracts → arch tests stay green (33).
- **Files:** `src/Modules/Geo/WhereToEat.Geo.Application` (IGeocoder, GeocodeRestaurantCommand+Handler,
  GeocodeOnAddressChangedHandler, GeoDistance reusing SharedKernel.Haversine);
  `src/Modules/Geo/WhereToEat.Geo.Infrastructure` (NominatimGeocoder, NominatimOptions,
  NominatimResponse, AddGeoModule). Rate-limit is enforced INSIDE the geocoder (SemaphoreSlim +
  TimeProvider, default TimeProvider.System) — a per-request Polly handler can't serialise sends —
  so the geocoder is registered as a **singleton** (process-wide interval). Polly handles retry only.
- **Logging convention established** (first ILogger usage in src/): codebase enforces **CA1848**, so
  use source-generated `[LoggerMessage]` partial methods on a `partial` class. Future steps: do the same.
- **Packages added** to Directory.Packages.props: `Microsoft.Extensions.Http` 8.0.1,
  `Microsoft.Extensions.Http.Polly` 8.0.11, **`Polly.Extensions.Http` 3.0.0** (HttpPolicyExtensions
  is in THIS package, not Microsoft.Extensions.Http.Polly), `Microsoft.Extensions.Options` 8.0.2,
  `Microsoft.Extensions.Logging.Abstractions` 8.0.2.
- **appsettings** Nominatim section added to PublicApi host (Worker host arrives in Step 12).
- **Invariant #7**: no Google-lat/lng path — geocoder returns only OSM point; CatalogCoordinateWriter
  rebuilds OSM-only `Coordinates` VO (no Google-coords factory exists). Geo integration test asserts
  the value lands in the SQL `geography` column (`catalog.Restaurant.Location`).
- **Tests:** Geo.UnitTests 15 (mocked HttpMessageHandler + pinned fixture; rate-limit timing via
  Stopwatch + real TimeProvider with small intervals; error/empty/out-of-range failures; handler
  tests with NSubstitute), Geo.Integration 3 (Testcontainers SQL; stub IGeocoder, no live calls).
- **Results:** build 0 warn/0 err; full suite **314 passed / 0 failed / 0 skipped**.

### Step 8 dev-reviewer revision (rate-limit must actually be process-wide + cover retries)
- **Issue 1 (per-instance limiter):** typed-client `NominatimGeocoder` is transient, so its old
  per-instance `SemaphoreSlim` was NOT process-wide. Fixed: rate state moved OUT of the geocoder into
  a **singleton `NominatimRateLimiter`** (`AddSingleton`) — one semaphore + last-request timestamp +
  TimeProvider for the whole process. Geocoder now holds NO rate state (stays a transient typed
  client, no longer `IDisposable`); constructor is `(HttpClient, ILogger)`.
- **Issue 2 (Polly bypassed the gate):** added **`RateLimitingHandler : DelegatingHandler`** (transient)
  that `AcquireAsync`/dispose-leases around `base.SendAsync`. Pipeline ordering in `AddGeoModule`:
  `.AddPolicyHandler(BuildRetryPolicy)` registered FIRST (OUTER) then `.AddHttpMessageHandler<RateLimitingHandler>()`
  (INNER) → each Polly retry re-invokes the inner handler and re-enters the limiter, so **every attempt
  re-throttles**, not just the first. Limiter lease stamps last-request time + releases the semaphore
  in the lease's `DisposeAsync` (handler disposes in `finally`); interval wait uses
  `Task.Delay(remaining, TimeProvider, ct)`. `NominatimRateLimiter` is now non-sealed (full Dispose
  pattern w/ GC.SuppressFinalize to satisfy CA1816) with `virtual AcquireAsync` so tests can count entries.
- **Issue 3 (flaky wall-clock test):** added pkg `Microsoft.Extensions.TimeProvider.Testing` 8.10.0
  (central). New `NominatimRateLimiterTests` uses `FakeTimeProvider` — deterministic, no sleeps:
  (a) first acquire not delayed; (b) second acquire GATED until `clock.Advance` crosses the interval;
  (c) a 503 + Polly retries pipeline (TestRetryHandler OUTER, RateLimitingHandler INNER) proves
  `AcquireCount == attempts` so every retry passes the limiter. Removed the two Stopwatch rate-limit
  tests from `NominatimGeocoderTests`. **AVOID** driving FakeTimeProvider via a background busy-wait
  pump — it hangs `dotnet test`; advance the clock synchronously on the test thread instead.
- **Results (revision):** build 0 warn/0 err; full suite **315 passed / 0 failed / 0 skipped** across
  17 test assemblies (Geo.UnitTests now 16; Geo.Integration 3 green on Docker; arch 33). No live
  Nominatim. Invariant #7 path unchanged; module-isolation (rule d) still green.

## Step 9 — Parsing module (crash-recovery completed, awaiting QA)
- **Finished the missing infra** on top of the prior session's skeleton: `Normalizer.cs` (raw dish →
  canonical via Contracts `ICatalogDishResolver`; null → quarantine, never drop/fatal; TimeProvider
  injected), `Compliance/HostRateLimiter.cs` + `HostRateLimitOptions`, `Persistence/DapperParseQuarantineStore.cs`
  + `ParseQuarantineSql.cs`, `Strategies/ChromeWebDriverFactory.cs` (prod headless), `Examples/{BorschCafeSelenium,PizzaHouseAngleSharp}Parser.cs`
  (thin keyed wrappers over the shared strategies), `AddParsingModule.cs`.
- **Migration `0011_create_parse_quarantine.sql`** = `parsing.ParseQuarantine` (own schema, no
  cross-module FK). DbUp auto-embeds `db/migrations/*.sql` by glob — no csproj edit for new scripts.
- **DESIGN DIVERGENCE (documented in code):** plan named `RedisHostRateLimiter`; prior session had
  already split it into `IHostRateLimiter` (Application port) + swappable `IRateLimitCounterStore`
  (in-memory now / Redis-in-Step-13 behind cache abstraction). Added `HostRateLimiter` (host-keyed gate
  on the counter-store seam) instead of a Redis-named class. Same behavior, the counter store is the
  Redis-vs-memory swap point.
- **Pre-existing mid-session bugs I had to fix to compile/run (in scope):**
  - `SeleniumParserStrategy.cs` `[LoggerMessage(Level = LogLevel.Warning)]` — `OpenQA.Selenium` also
    defines `LogLevel` → ambiguous. Fully-qualified `Microsoft.Extensions.Logging.LogLevel`.
  - `ParserStrategySelector._byKey` typed `IReadOnlyDictionary` → CA1859 (warnings-as-errors) → `Dictionary`.
  - **`AngleSharpParserStrategy`**: default network loader can't load `file://` portably (NRE, FLAKY).
    For `source.Url.IsFile` it now reads the file and `OpenAsync(req => req.Content(html))`; http(s)
    still uses the loader. This is what made the file-fixture tests CI-stable.
- **Selenium e2e RUNS HERE** (`tests/E2E/WhereToEat.Parsing.E2E`): real headless Chrome (Selenium
  Manager auto-resolves driver) drives `SeleniumParserStrategy` against `Fixtures/borsch-cafe/menu.html`
  via `file://`, ~18s. Harness `SeleniumParserHarness.ParseFixtureAsync(name, fixturePath)` — "new
  restaurant = new fixture + copy of `BorschCafeParserTests`". GOTCHA: a live browser normalizes a bare
  `href` to absolute (trailing slash), so assert host prefix not exact URL bytes.
- **Fixtures** follow the shared `MenuHtmlConvention`: `[data-restaurant]` root
  (`data-name/-address/-city/-currency`), `[data-contact]`, `[data-dish]` rows (`.name/.price/.weight`
  + `data-category`). borsch-cafe deliberately has the unmappable dish "Узвар" to exercise quarantine;
  it's copied into the Integration project for the Testcontainers quarantine test.
- **Arch tests:** added Parsing.{Domain,Application,Infrastructure} ProjectReferences to
  ArchitectureTests.csproj — module-isolation rule (d) stays green (Parsing → Catalog/Geo only via
  Contracts seams `ICatalogDishResolver`/`ICatalogMenuWriter`/`IRestaurantGeocoder`). 33/33.
- **Tests:** Parsing.UnitTests **22** (FirstPartyAllowList, HostRateLimiter w/ FakeTimeProvider,
  RobotsTxtGate w/ stub handler, Normalizer split, RunParseCommandHandler — compliance-before-fetch +
  admin protection + quarantine-even-when-DoNotUpdate); Parsing.Integration **3** (AngleSharp contract,
  migration-table, Testcontainers quarantine: unmappable→table + mappable still persist + parse not
  failed); Parsing.E2E **1** (real headless Chrome). Puppeteer seam declared-only (no impl anywhere).
- **Results:** `dotnet build src\WhereToEat.sln` 0 warn / 0 err; full solution test run 0 failed /
  0 skipped (arch 33). Step left `[~] in-progress` for QA.

## Step 10 (Admin API host + EF Core database-first CRUD + protection flags)
- **EF contained to `WhereToEat.Admin.Infrastructure` only.** Added EF Core 8.0.11 + SqlServer to
  `Directory.Packages.props`. Step 2 EF-containment test now bites for real and is GREEN (arch 33/33).
- **Admin.Application:** `IAdminCatalogStore` port + `IAddressChangedDispatcher` seam; commands +
  handlers (EditMenuItem, SetMenuItemDoNotParse, SetRestaurantDoNotUpdate, EditAddress,
  ManageRealPhoto). No EF dep (rule b). **Admin.Infrastructure:** `AdminDbContext` (database-first,
  applies `Configurations/*.cs`), OWN EF entities over shared tables (`catalog.Restaurant/MenuItem/
  Dish/Photo`, `admin.RestaurantProtection`) — no Catalog.Domain ref (rule d). `EfAdminCatalogStore`,
  `EfMigrationsDisabledGuard`, `AddAdminModule`.
- **EF migrations disabled:** no Migrations folder. Guard asserts (1) zero EF migrations, (2) every
  mapped table exists (else fail fast); host runs `EnsureNoSchemaDriftOrThrow` at startup. Reads only
  INFORMATION_SCHEMA — never alters schema.
- **Admin-only authz:** `AdminAuthorization.cs` in BuildingBlocks.Auth holds the single policy
  (RequireAuthenticatedUser + RequireAssertion(IsAdmin via RoleClaimMapper)).
  `AuthServiceCollectionExtensions` delegates to it. admin 200 / user 403 / anon 401 via test key.
- **EditAddress→re-geocode:** handler dispatches AFTER successful persist; admin host wires
  `InProcessAddressChangedDispatcher` → `RestaurantAddressChanged` → Step 8 handler. Observed by
  substituting `IGeocoder` via ConfigureTestServices.
- **E2E protection test:** test refs Admin + Parsing; Dapper test doubles drive real Step 9
  `RunParseCommandHandler` with a `StubParserStrategy` — proves DoNotUpdate skips venue, DoNotParse
  shields item while unprotected item still overwritten.
- **Gotchas:** Testcontainers MsSql connection uses `Database=master`; DbUp creates schema there, so
  the "missing table" guard test points at `tempdb`. `IRestaurantParser.StrategyKey` (not `Key`).
- **Results:** build 0 warn / 0 err. Admin unit 7/7, Admin integration 15/15, arch 33/33,
  PublicApi integration 21/21 (auth-refactor regression check), all other unit projects green.
  Step left `[~] in-progress` for QA.

## Step 11 state (finalized, awaiting QA)
- New project `WhereToEat.Analytics.Application`: `RawAnalyticsEvent` (the only place raw id/precise
  lat/lng exist, in-memory), `IAnonymizer`, `ISaltProvider`, `IngestEventCommand`+handler,
  `IAnalyticsRollupReader`+`RestaurantHourlyRollup`. References only Analytics.Domain + Logging.Abstractions.
- `WhereToEat.Analytics.Infrastructure` adds `Anonymizer` (HMAC-SHA256 keyed by rotating salt -> 64-hex
  digest; coarsens time->HourBucket, lat/lng->Geohash; drops raw id/precise coords), `RotatingSaltProvider`
  (salt = masterSecret:windowIndex, windowIndex = UtcTicks/windowTicks -> stable in-window, divergent
  cross-window), `GeohashEncoder` (standard base-32, precision clamped to Geohash.MaxPrecision=6),
  `AnalyticsRollupReader` (GROUP BY over events, aggregates only), `AddAnalyticsModule`. Now references
  Analytics.Application too.
- **Extended Step 5 domain** `EventDimensions` with an optional retained `RestaurantId` (venue id, not a
  person) so the per-restaurant rollup can group; updated `EventDimensionsDocument` accordingly. `Create`
  signature gained a leading `restaurantId` param — existing callers use named args so unaffected.
- Migration `0012_create_analytics_rollups.sql`: `analytics.RestaurantHourlyRollup` table (materialized
  destination seam for Step 12). The Step 11 reader computes the rollup by grouping the event store on
  read (proves the path without needing a populate job).
- Endpoint `POST /analytics/events` on PublicApi (anonymous, 202 on success, 400 on unknown kind /
  out-of-range coord / empty batch). Salt master secret from `Analytics:SaltMasterSecret` config.
- Architecture tests: added `WhereToEat.Analytics.Application` ProjectReference (Infrastructure already
  present). Fitness rules stay green.
- Rollup SQL pulls restaurant id from JSON via `JSON_VALUE(DimensionsJson,'$.restaurantId')` (camelCase,
  Web serializer). Kind 0=Impression, 2=CardOpen for CTR.
- **Results:** build 0 warn / 0 err. Analytics.Application unit 18/18, arch 33/33, Analytics integration
  9/9 (isolated), PublicApi integration 25/25 (isolated), full parallel sln run: 0 failures (no flake
  this run). Step left `[~] in-progress` for QA.

## Step 11 revision (reviewer needs_revision -> 3 fixes)
- **HIGH (blank master secret):** `AddAnalyticsModule` gained a required `environmentName` param + a
  fail-fast guard — outside Development/Testing a null/whitespace `MasterSecret` throws
  `InvalidOperationException` (mirrors the OIDC authority guard in BuildingBlocks.Auth; Dev/Testing
  tolerate blank). HMAC stays keyed by `MasterSecret`+rotating salt (salt = `masterSecret:windowIndex`),
  so a real secret is mandatory in prod. Caller passes `builder.Environment.EnvironmentName`. New unit
  file `AddAnalyticsModuleGuardTests` (6 theory cases). Added central pkg `Microsoft.Extensions.
  DependencyInjection` 8.0.1 + reference in the Analytics unit test proj (for `new ServiceCollection()`).
- **MEDIUM (contract nullability):** `AnalyticsEventRaised.CoarseGeohash` + `HashedActorId` now `string?`
  to match domain optionality (most events are location-/actor-less) — avoids `null!` at Step 13 publish.
- **LOW (weak PII test):** widened the negative PII integration check to the FULL coords (`49.8397`/
  `24.0297`) + raw id across ALL textual columns (ActorHash, CoarseGeohash, DimensionsJson, and
  defensively `CAST(Id AS VARCHAR(64))`).
- **Results:** build 0 warn / 0 err. Analytics unit 24/24 (was 18 + 6 guard), arch 33/33, Analytics
  integration 9/9. Full parallel sln run: PublicApi(25)/Geo(3) failed on Testcontainers Docker-parallel
  flake (`Docker.DotNet.MakeRequestAsync`, ~1ms, never hit test logic); BOTH green in isolation. Step
  stays `[~] in-progress` for QA.

## Step 12 (Worker host + Quartz clustered + 4 jobs) — done, awaiting QA
- New host `src/Hosts/WhereToEat.Worker` (Microsoft.NET.Sdk.Worker). Composition split into
  `WorkerCompositionRoot.AddWorkerServices` (+ `AddJobDependencies`) so tests wire the same DI.
  `Program.cs` runs `MigrationRunner.Run` on startup then `AddQuartzHostedService`.
- Quartz packages added centrally: `Quartz` + `Quartz.Extensions.Hosting` 3.13.1. NO JSON serializer
  pkg cached — used default + `store.UseProperties = true` (jobs carry no JobDataMap, so cluster
  election via QRTZ_LOCKS/fired-triggers is unaffected). `UseSystemTextJsonSerializer` is NOT in 3.13's
  base Quartz assembly (needs a separate pkg) — do not call it.
- Clustered store: `quartz.UsePersistentStore(store.UseClustering(...).UseSqlServer(...))`, shared
  `SchedulerName`, auto InstanceId. Migration `db/migrations/0013_quartz_cluster_tables.sql` = standard
  QRTZ_* tables made idempotent (IF OBJECT_ID / sys.foreign_keys / sys.indexes guards). Embedded
  automatically by the glob in BuildingBlocks.Migrations.csproj.
- Jobs: `WeeklyParseJob` (delegates to Step 9 RunParseCommandHandler per `IParseSourceProvider` source),
  `GeocodeRefreshJob` (drains "needs geocode" via new `ICatalogCoordinateWriter.GetRestaurantIdsNeedingGeocodeAsync`
  + Step 8 GeocodeRestaurantCommandHandler), `NightlyPriceMedianJob` (IPriceMedianWriter, DB-only),
  `RatingRecomputeJob` (IRatingAggregateRecomputer). All `[DisallowConcurrentExecution]`, correlation id
  = `context.FireInstanceId`, `[LoggerMessage]` logs (CA1848). `Scheduling/JobSchedule.cs` = cron wiring.
- Median: `PriceMedianCalculator` (pure: per-area median when sample>=N, else city-wide '*' fallback;
  reuses `AreaKey.FromCoordinates` grid so written rows == what `DbPriceMedianProvider` reads) +
  `PriceMedianWriter` (Dapper read of priced items joined to restaurant Location + dish category, then
  transactional DELETE+INSERT replace). `IPriceMedianWriter` registered in `AddRecommendationModule`.
- Rating: `DapperRatingAggregateRecomputer` (SUM/COUNT_BIG group-by from ratings.Rating -> upsert via
  existing repo MERGE). `IRatingAggregateRecomputer` registered in `AddRatingsPersistence`. Smoothed
  value = SharedKernel `BayesianRatingSmoothing` over materialized sum/count (same formula the engine uses).
- BREAKING (fixed): `CatalogCoordinateWriter` ctor now takes `(ICatalogRepository, ISqlConnectionFactory)`.
  Updated the one direct call site in `tests/Integration/WhereToEat.Geo.Integration/GeocodePersistenceTests.cs`.
- Tests: `tests/Unit/WhereToEat.Worker.UnitTests` (PriceMedianCalculator + JobSchedule cron, 10/10);
  `tests/Integration/WhereToEat.Worker.Integration` (Testcontainers, shared 1 container via
  `[CollectionDefinition("worker-sql")]` -> avoids parallel-startup flake): single-fire-under-clustering
  (TWO StdSchedulerFactory schedulers, same DB store, one-shot trigger, count==1), nightly median
  (provider reads back exact rows incl. city-wide fallback), rating-recompute (matches Bayesian formula).
  3/3 green in isolation.
- Results: full sln build 0 warn / 0 err. arch 33/33. Worker unit 10/10, Worker integration 3/3,
  Geo integration 3/3, Catalog integration 7/7, Recommendation domain 39/39, Ratings domain 29/29.
  Step left `[~] in-progress` for QA.

## Step 13 state (finalized, awaiting QA)
- New shared modules (Caching/Messaging/Observability) added to src/WhereToEat.sln (00-Shared) and to
  the architecture-tests csproj. Caching/Messaging needed Microsoft.Extensions.Configuration.Binder
  added (for `.Bind`); also pinned Microsoft.Extensions.Configuration (for AddInMemoryCollection in tests).
- OpenTelemetry pinned to **1.9.0** (not 1.10) — 1.10 pulls Microsoft.Extensions.* 9.0.0 transitively
  which collides with the solution's 8.x central pins under transitive pinning (NU1109). 1.9 stays on
  8.x. The OTLP wire exporter is OMITTED (1.9.0 OTLP exporter has advisory GHSA-4625 → NU1902 under
  warnings-as-errors); AddObservability uses the **Console exporter** gated on `OpenTelemetry:ConsoleExporter`.
- AddObservability has TWO overloads: `AddObservability` (web: ASP.NET instrumentation + correlation
  middleware) and `AddWorkerObservability` (generic host: no ASP.NET). Worker uses the latter.
- All 3 hosts wire AddObservability/AddWorkerObservability + AddRedisCache + AddMessaging; PublicApi
  gained Geo refs so the bus geocode consumer resolves. UseCorrelationId() added to both web hosts.
- CachingPriceMedianProvider decorator already registered in AddRecommendationModule (resolves to DB
  provider when cache is Null, decorator when a real ICacheService present) — transparent, Step 7 unchanged.
- Item A deferral CLOSED: AddCatalogPersistence registers ICatalogDishResolver/ICatalogMenuWriter,
  AddGeoModule registers IRestaurantGeocoder (both already present from prior session; fixed missing
  usings in AddGeoModule). WeeklyParseJobResolutionTests proves the full graph resolves (no Docker).
- deploy/: Dockerfile.{PublicApi,AdminApi,Worker}, docker-compose.yml (SQL/Redis/RabbitMQ/Keycloak +
  3 hosts + one-shot `migrations` service), .env.example (replica/resource knobs), keycloak/realm-export.json
  (admin+user realm roles + client scopes, wheretoeat-api client, admin-test/user-test users).
- Item B FIXED: tests/WhereToEat.runsettings (MaxCpuCount=1 serializes assemblies) + tests/xunit.runner.json
  (parallelizeAssembly/Collections false) copied into every test output via tests/Directory.Build.targets.
  IMPORTANT: XML comments in .runsettings must NOT contain `--` (it errors). Run the full suite with:
    dotnet test src/WhereToEat.sln --settings tests/WhereToEat.runsettings
- Redundant AdminApi.Integration test doubles (Parsing/DapperCatalog*.cs) LEFT as-is: they are
  constructed directly (not via DI), don't conflict with production registrations, and keep that test green.
- Results: full sln build 0 warn / 0 err. FULL `dotnet test` one invocation (with runsettings):
  **425 passed / 0 failed / 0 skipped** across 26 assemblies, reliably green over 2 runs (Redis/RabbitMQ
  images first-pulled). Step left `[~] in-progress` for QA.
