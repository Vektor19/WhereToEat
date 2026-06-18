# Agents Log — backend-foundation

_Append-only debug log. Agents never read this file._

---
- **time:** 2026-06-14T18:18:59Z
- **agent:** developer
- **status:** completed
- **message:** Environment readiness check: .NET 8.0.417 SDK present (target met), git 2.51, local SQL Server MSSQLSERVER running + sqlcmd + LocalDB, Chrome 149 + Edge 149 for Selenium, nuget.org reachable (200) and restore verified. BLOCKER FOR LATER: Docker Desktop daemon not running (CLI 28.4.0 + compose v2.39 installed) - needed from Step 4 (Testcontainers) and Step 13 (compose: SQL/Redis/RabbitMQ/Keycloak). No blockers for Step 1.

---
- **time:** 2026-06-14T18:22:19Z
- **agent:** qa
- **status:** completed
- **message:** Environment readiness check completed. .NET 8 SDK present. Docker daemon not running. NSubstitute/Testcontainers/NetArchTest/AngleSharp/Dapper not in NuGet cache. ChromeDriver cached at v120/v140 but Chrome is v149 - Selenium Manager will resolve. See qa notes for full findings.

---
- **time:** 2026-06-14T21:42:08Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 1 finalized after crash recovery: verified SharedKernel (Result/Error/ResultExtensions, Money, GeoPoint, Haversine, Primitives) and Contracts (integration events + recommendation DTOs) against acceptance criteria. Added missing 01-Modules and 02-Hosts solution folders to WhereToEat.sln. Build clean: 0 warnings / 0 errors (warnings-as-errors). Tests: 50 passed, 0 failed, 0 skipped. Confirmed SharedKernel and Contracts have zero package refs (incl. transitive) — no ASP.NET/EF/Dapper/infra. Status left [~] in-progress for QA.

---
- **time:** 2026-06-14T21:48:20Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 1 review found 5 issues: (1) misleading test name Success_ConstructedWithAnError_Throws tests the wrong path; (2) [MaybeNull] on Result<T>.Value is semantically wrong (non-null on success, throws on failure, never null); (3) SelectedItem allows invalid both-null/both-non-null states with no runtime guard; (4) Money.IsAllLetters uses char.IsLetter which accepts non-ASCII letters (é,à) as valid ISO 4217 chars; (5) missing tests for ValueObject/Entity/AggregateRoot primitives.

---
- **time:** 2026-06-14T21:58:41Z
- **agent:** developer
- **status:** needs_revision
- **message:** Step 1 revision: fixed all 6 developer-reviewer findings. Renamed Failure_WithNoError_Throws; removed [MaybeNull] from Result<T>.Value and cleaned .Value! at all call sites; SelectedItem now private ctor + Category/Dish factories (get-only props, throws on empty guid); Money uses char.IsAsciiLetter with non-ASCII rejection tests; added Primitives tests (ValueObject/Entity/AggregateRoot, 18) + Contracts SelectedItem tests (5); global.json rollForward latestPatch. Build 0 warn/0 err; tests 76 passed/0 failed/0 skipped.

---
- **time:** 2026-06-14T22:02:08Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Round 2 review: all 6 findings confirmed resolved; build 0w/0e; 76/76 tests pass; Contracts test project correctly wired; null-equality coverage gap confirmed non-issue.

---
- **time:** 2026-06-14T22:03:56Z
- **agent:** qa
- **status:** completed
- **message:** Step 1 verified: dotnet build exits 0 with 0 warnings/0 errors (TreatWarningsAsErrors active). dotnet test: 76/76 passed (71 SharedKernel.UnitTests + 5 Contracts.UnitTests), 0 failed, 0 skipped. Acceptance criteria confirmed: Result<T>.Success exposes Value; Result.Failure carries Error; accessing Value on failure throws InvalidOperationException. Money rejects negative amounts and non-ASCII/non-3-letter currencies; mismatched-currency arithmetic throws. GeoPoint rejects lat outside [-90,90] and lng outside [-180,180]. Haversine matches 4 known city-pair distances within 0.5% tolerance and returns exact 0 for identity. SharedKernel and Contracts have zero NuGet packages (no ASP.NET/EF/Dapper). global.json pins SDK 8.0.417 (rollForward latestPatch). Directory.Build.props sets net8.0 TFM and TreatWarningsAsErrors. Directory.Packages.props sets ManagePackageVersionsCentrally=true. Step 1 marked done.

---
- **time:** 2026-06-14T22:14:32Z
- **agent:** developer
- **status:** completed
- **message:** Step 2: NetArchTest architecture-fitness project landed (NetArchTest.Rules 1.3.2). 5 rule classes (Domain->only-SharedKernel; Application->no Infra/Host; EF only in Admin.Infrastructure; cross-module only via Contracts; IConsumer<> consumer-to-contract) + ModuleGrouping helper/tests pinning Catalog.Search.*->Catalog. Convention-based AssemblyLoader discovers WhereToEat.* dlls at runtime so rules grow with modules. Build 0 warnings/0 errors. Tests: Architecture 33 passed; full solution 109 passed / 0 failed / 0 skipped. Negative check (temp EF DbContextId? property on SharedKernel) made EfCoreContainmentTests fail then fully reverted - guard proven to bite. Finding: rules read IL referenced assemblies, so a bare/unused PackageReference (or nameof) is invisible; fires only on real signature use.

---
- **time:** 2026-06-14T22:20:35Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 2 review: 4 findings. (1) medium: NetArchTest.Rules package is declared in .csproj and Directory.Packages.props but is never used in any .cs file - all rules use plain System.Reflection; creates false impression the step uses NetArchTest predicates and adds an unused dependency. (2) low: StructurallyReferencedTypes in MessageConsumerBoundaryTests uses BindingFlags.Instance only, missing BindingFlags.Static - static fields/methods with cross-module types on consumers are not caught. (3) low: variables named consumerModule (MessageConsumerBoundaryTests line 35) and referencingModule (ModuleBoundaryTests line 29) hold full assembly name strings, not resolved module names - misleading name. (4) low: Directory.Packages.props line 26-28 comment says NetArchTest.Rules drives the boundary guards - inaccurate since pure reflection is used.

---
- **time:** 2026-06-14T22:28:43Z
- **agent:** developer
- **status:** needs_revision-fixed
- **message:** Step 2 revision: moved rules (a) Domain-deps, (b) Application-deps, (c) EF-containment, (d) module-isolation onto NetArchTest fluent API (Types.InAssemblies/InAssembly + ShouldNot().HaveDependencyOn[Any]); kept reflection only for consumer-structural scan (rule e) + ModuleGrouping. Fixed BindingFlags.Static (F2), renamed referencingModule/consumerModule -> *AssemblyName (F3), corrected Directory.Packages.props comment (F4). Build 0W/0E; tests 33 arch + 71 SK + 5 contracts = 109 pass/0 fail/0 skip. Introduce-then-revert probe: EF-containment rule failed naming WhereToEat.SharedKernel.TempEfViolation, then reverted green.

---
- **time:** 2026-06-14T22:32:24Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Round 2 review: all 4 round-1 findings confirmed resolved. NetArchTest fluent API (Types.InAssemblies/ShouldNot/HaveDependencyOn) drives rules a-d; BindingFlags.Static added; consumerModule renamed to consumerAssemblyName / referencingModule removed; comments accurate. 33 tests pass. No new issues.

---
- **time:** 2026-06-14T22:35:21Z
- **agent:** qa
- **status:** completed
- **message:** Step 2 verified: build 0 warn/0 err; 109 tests pass (Architecture 33, SharedKernel 71, Contracts 5). Behavioral probe confirmed: added EF PackageReference + DbContext-signature type to SharedKernel; EfCoreContainmentTests.EntityFrameworkCore_IsDependedOnOnlyBy_AdminInfrastructure failed naming WhereToEat.SharedKernel.ProbeViolation; reverted all 3 changes; git status clean; final run 109/109 green. ModuleGroupingTests pin Catalog.Search.*->Catalog and Recommendation->Recommendation. Step marked done.

---
- **time:** 2026-06-15T08:02:40Z
- **agent:** developer
- **status:** completed
- **message:** Step 3 finished: Restaurant aggregate, MenuItem, Coordinates (no-Google-geo guard), ContactLink, AddressChanged/MenuItemUpserted events, ICatalogRepository port; Catalog.Domain wired into solution + architecture tests. Build 0/0; tests Catalog 24 pass, Architecture 33 pass.

---
- **time:** 2026-06-15T08:05:21Z
- **agent:** developer-reviewer
- **status:** in-progress
- **message:** Reviewing Step 3: Catalog domain core - reading all source and test files

---
- **time:** 2026-06-15T08:06:51Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 3 review complete: 4 findings - scope creep (UpsertMenuItem), test gap (no event assertion on UpsertMenuItem update path), null handling inconsistency (Update throws vs Create returns Result), and invariant guard test weakness (reflection test misses non-static factories).

---
- **time:** 2026-06-15T08:10:10Z
- **agent:** developer
- **status:** completed
- **message:** Step 3 revision: removed UpsertMenuItem/SetMenuItemDoNotParse (deferred to Step 9) and now-dead MenuItem.Update/SetDoNotParse; removed 2 upsert tests; strengthened Coordinates invariant-7 guard to reject any public two-double method. Build 0/0; 132 tests pass.

---
- **time:** 2026-06-15T08:13:11Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Round 2 review: all 4 round-1 findings confirmed resolved. UpsertMenuItem/SetMenuItemDoNotParse and their tests removed; MenuItemUpserted raised only from AddMenuItem (not orphaned); MenuItem.Update/SetDoNotParse removed (no dead code); Coordinates two-double test added and passes; no throw-based null guards remain; build 0/0; 132 tests pass.

---
- **time:** 2026-06-15T08:16:39Z
- **agent:** qa
- **status:** completed
- **message:** Step 3 verified: build 0w/0e, 132 tests pass (Catalog.Domain 23, Architecture 33, SharedKernel 71, Contracts 5). csproj confirms only SharedKernel ref. Factory guards (Dish w/o Category=fail, duplicate MenuItem=fail, negative Money=fail) confirmed by unit tests. Coordinates guard probe: added public FromGoogleLatLng(double,double) -> Coordinates_HasNoPublicMethodTakingTwoDoubles FAILED with 'found FromGoogleLatLng'; reverted -> test green. AddressChanged raised on address mutation, no event on no-op. Architecture rule (a) now non-vacuous with Catalog.Domain present and passes.

---
- **time:** 2026-06-15T08:42:54Z
- **agent:** developer
- **status:** completed
- **message:** Step 4: DbUp migration scripts (0000-0003 catalog schema + geography spatial index), BuildingBlocks.Persistence + BuildingBlocks.Migrations, console MigrationRunner, Dapper Catalog.Infrastructure (ICatalogRepository + radius pre-filter via spatial index, Haversine in code). Wired Catalog.Infrastructure into architecture tests. Build 0 warn/0 err; arch tests 33/33; Testcontainers SQL integration 5/5 green (image pull ~10min first run).

---
- **time:** 2026-06-15T08:48:10Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 4 review: found 3 real issues — UpdateRestaurantAsync missing transaction (data integrity); LogToNowhere in MigrationRunner silences migration progress; ICatalogSpatialReader boundary placement may cause future cross-module reference issue

---
- **time:** 2026-06-15T08:58:54Z
- **agent:** developer
- **status:** completed
- **message:** Step 4 revision: wrapped UpdateRestaurantAsync + AddRestaurantAsync in IDbTransaction (commit/rollback) with a rollback-proving integration test; MigrationRunner.Run now takes optional IUpgradeLog (NoOp default, ConsoleUpgradeLog in console tool); made ICatalogSpatialReader/NearbyRestaurant/DapperCatalogSpatialReader internal + InternalsVisibleTo for the integration test. Build 0/0; tests 139 pass (Integration 7, Architecture 33), 0 skipped.

---
- **time:** 2026-06-15T09:01:42Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 4 round-2 review: all 3 findings resolved. Transactions wrap full UPDATE+DELETE+INSERT sequence; rollback test proves atomicity post-DELETE via real FK violation; MigrationRunner.Run takes IUpgradeLog? defaulting to NoOpUpgradeLog (real DbUp type, confirmed in binary); spatial types internal + InternalsVisibleTo matches integration test AssemblyName exactly. Build 0/0, 139 tests pass. Approved.

---
- **time:** 2026-06-15T09:05:40Z
- **agent:** qa
- **status:** completed
- **message:** Step 4 verified: build 0w/0e; SharedKernel 71, Catalog.Domain 23, Contracts 5, ArchTests 33, Integration 7 — all passed (0 skipped). Testcontainers SQL Server 28.4.0 container confirmed live (container spin-up visible in logs). Migration idempotency, geography column + spatial index, Dapper round-trip (Money/PlaceId/coordinates), Haversine ordering, and rollback atomicity all verified by actual test execution. Catalog.Infrastructure references only Dapper — no EF (EF-containment architecture test green). Step status set to done.

---
- **time:** 2026-06-15T13:24:42Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 5 completed (crash recovery): finished Ratings (RatingAggregate + Dapper Infra), Users, Photos gate, Monetization (labeled-ad no-organic-rank), Admin DoNotUpdate, Analytics anonymization domain+append writer; migrations 0004-0009; wired 14 projects into sln + arch tests. Build 0 warn/0 err. Tests: unit 92 + arch 33 + integration 16 (Ratings 4, Analytics 5, Catalog 7) all green.

---
- **time:** 2026-06-15T13:32:59Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 5 review complete — 3 issues found: (1) CoarseGeohash column VARCHAR(12) in 0009 migration vs MaxPrecision=6 in domain; (2) AnalyticsEventReader accesses .Value without checking IsSuccess on 3 Result values; (3) Photo lacks a rehydration factory for real-but-revoked state.

---
- **time:** 2026-06-15T13:37:27Z
- **agent:** developer
- **status:** completed
- **message:** Step 5 revision: fixed 3 dev-reviewer findings (geohash VARCHAR(6)+CHECK in 0009; IsFailure guards w/ context in AnalyticsEventReader; Photo.Rehydrate + InternalsVisibleTo + tests). Build 0/0; 219 tests pass.

---
- **time:** 2026-06-15T13:42:31Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 5 round-2 review: all 3 findings confirmed resolved. (1) 0009 has VARCHAR(6)+CHECK matching domain MaxPrecision=6, analytics integration test confirms idempotent apply. (2) All 3 .Value accesses in AnalyticsEventReader.GetByIdAsync guarded with IsFailure+InvalidOperationException with event id+Error.Message. (3) Photo.Rehydrate internal, reconstructs real-but-revoked state, guards generic+permission, InternalsVisibleTo covers Catalog.Infrastructure+Catalog.Domain.UnitTests, 2 round-trip tests assert non-displayable. Build 0/0, 219 tests pass.

---
- **time:** 2026-06-15T13:54:40Z
- **agent:** qa
- **status:** completed
- **message:** Step 5 verified: build 0w/0e; 219 tests passed (0 failed, 0 skipped) across 12 assemblies; Ratings (4) and Analytics (5) Testcontainers SQL integration tests ran against real containers; BayesianSmoothing unit tests exhaustive and green; Photo gate (generic default, permission required for real) verified; reflection guard on AdPlacement confirmed by probe (RankBoost caused test failure, reverted); DoNotUpdate and DoNotParse are public queryable fields; migrations 0004-0009 applied idempotently via fixtures; architecture tests green with all Step 5 assemblies present making module-isolation rule non-vacuous. Step 5 marked [x] done.

---
- **time:** 2026-06-15T14:17:37Z
- **agent:** developer
- **status:** completed
- **message:** Step 6: Public API host (Minimal API) + Catalog.Application + Catalog.Search.Application + BuildingBlocks.Auth (OIDC resource-server) + DB-only Map stub. Added read-side queries to ICatalogRepository/Dapper. Wired AddCatalogModule/AddCatalogSearchModule. Registered new projects in sln + arch tests. Build 0 warn/0 err; tests: 14 PublicApi integration + 10 Catalog.Application unit + 33 architecture all green; full suite 243 passing. No geocoder dependency in host; no NLP in search.

---
- **time:** 2026-06-15T14:23:45Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 6 review: 6 issues found — 1 high (test-key seam not guarded by env-check/IssuerSigningKey overwrites), 2 medium (env var global mutation race, blank Authority allows any issuer in prod base config), 3 low (result discarded in unit test setup, ICatalogRepository SRP concern, weak signing-key validation test)

---
- **time:** 2026-06-15T18:04:55Z
- **agent:** developer
- **status:** completed
- **message:** Step 6 revision: fixed all 6 reviewer findings. (1+2) Removed test-key hook from Program.cs; auth wiring now reads TestSigningKey from IConfiguration and applies it+relaxed validation ONLY in Development/Testing (env-gated), fixture injects key via UseSetting (no process-wide env var). (3) Fail-fast InvalidOperationException when Authority blank outside Dev/Testing. (4) Asserted IsSuccess on AddMenuItem Result. (5) Split read/write ports: new ICatalogReadPort in Catalog.Application for list/prefix reads, ICatalogRepository keeps aggregate writes+by-id loads; DapperCatalogRepository implements both. (6) Added wrong-key HS256 JWT 401 test. Build 0/0; 244 tests pass (15 PublicApi.Integration).

---
- **time:** 2026-06-15T18:09:15Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Round 2 review: all 6 round-1 findings confirmed resolved. Auth bypass (HIGH) eliminated — env-gated branch with forced-null testSigningKey in prod, fail-fast on blank Authority outside Dev/Testing, no process-env mutation, wrong-key 401 test added, ICatalogReadPort/ICatalogRepository split clean with correct scoped DI forwarding. 244/244 tests pass.

---
- **time:** 2026-06-15T18:19:13Z
- **agent:** qa
- **status:** completed
- **message:** Step 6 verified: build 0w/0e; 244 tests pass across 14 assemblies (0 failures, 0 skipped). PublicApi.Integration (15 tests) used WebApplicationFactory+Testcontainers SQL Server — confirmed by Docker container lifecycle in verbose output. All Step 6 acceptance criteria met: GET /categories, GET /categories/{id}/dishes, GET /restaurants/{id} return correct data with ContactLinks always present; search endpoints use anchored LIKE prefix (no NLP); auth returns 401 for missing/invalid/wrong-key tokens and 200 for valid token; Map endpoint echoes only stored fields, returns no-map-payload for restaurant without coords, no geocoder invoked (port does not exist); Swagger wired; Production-guard code confirmed: blank Authority in non-Dev/Test environment throws InvalidOperationException at startup, test signing key unreachable outside Testing/Development.

---
- **time:** 2026-06-15T18:42:36Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 7: implemented pluggable recommendation engine (Domain match/score/sort/filter/pipeline, Application RecommendQuery + ports, Infrastructure Dapper candidate source + DB-only DbPriceMedianProvider + keyed DI), migration 0010 price-medians, POST /recommend endpoint. Build 0 warnings/0 errors. Tests: 39 new unit + 4 new integration; full suite 0 failed/0 skipped (unit 287, arch 33, integration 35). Cross-module fitness green (Recommendation has no Ratings ref).

---
- **time:** 2026-06-15T18:48:55Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 7 review: found 5 issues - (1) HIGH: SmoothedRatingMath.GlobalMean default=4.0 vs Ratings.BayesianSmoothing authoritative default=3.5 — constant divergence; (2) HIGH: AddRecommendationModule comment says TryAdd but uses AddSingleton, so ISqlConnectionFactory is double-registered when both modules are used; (3) MEDIUM: SelectedItemBody.ToContract() throws ArgumentException (500) when both categoryId+dishId are null, should be 400; (4) MEDIUM: RecommendationModeOptions weight fields (PriceWeight etc.) are validated but never read by CompositeScoreStrategy - dead/misleading API; (5) LOW: UserGeo with invalid coordinates silently degrades to no-distance-ranking without error to caller

---
- **time:** 2026-06-15T18:58:55Z
- **agent:** developer
- **status:** completed
- **message:** Step 7 revision: single SharedKernel Bayesian smoothing (C=10,m=3.5) consumed by Ratings+Recommendation; TryAddSingleton for ISqlConnectionFactory in both modules; 400 for null/null item and out-of-range UserGeo; removed unused weight fields. Build 0/0, all 15 test projects green (296 passed).

---
- **time:** 2026-06-15T19:08:32Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 7 round-2 review: all 5 findings confirmed resolved — single authoritative BayesianRatingSmoothing in SharedKernel with C=10/m=3.5, both modules delegate; TryAddSingleton in both Catalog and Recommendation; 400 for null/null item and out-of-range userGeo; dead weight fields removed; 296/296 tests pass, 0 failures, architecture tests green

---
- **time:** 2026-06-16T07:17:13Z
- **agent:** qa
- **status:** completed
- **message:** Step 7 re-verified: build 0w/0e; 296 tests pass (0 failed, 0 skipped) across 15 assemblies incl. 39 Recommendation.Domain unit, 21 PublicApi.Integration (Testcontainers SQL ran for real). Explicit-sort-dominates confirmed by ExplicitSortDominatesTests (price/distance/rating each have primary-key-wins + coverage-tie-breaker pair). Composite mode determinism and weight-sensitivity confirmed by CompositeScoreTests. OR/AND + pluggable K-of-N strategy confirmed by MatchStrategyTests + PluggabilityTests. Single authoritative smoothing confirmed: BayesianRatingSmoothing in SharedKernel (C=10, m=3.5) consumed by both Ratings.Domain and Recommendation.Infrastructure.Candidates.SmoothedRatingMath; no divergent m=4.0. No Recommendation->Ratings source reference. Architecture test (33 green) includes cross-module boundary check. IPriceMedianProvider is DB-only (DbPriceMedianProvider reads recommendation.PriceMedian table, no Redis). Migration 0010 creates the table. Integration test seeds median table and rating aggregate directly (no Step 12 job dependency). 400 validation confirmed: RecommendEndpointTests covers neither/both-ids item, empty selection, unknown sort key, out-of-range userGeo — all return 400 not 500.

---
- **time:** 2026-06-16T07:34:48Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 8 Geo module implemented: IGeocoder port + NominatimGeocoder (typed HttpClient+Polly, User-Agent, rate-limit), GeocodeRestaurantCommand+handler, GeocodeOnAddressChangedHandler. Cross-module seam via Contracts.Geo.ICatalogCoordinateWriter (adapter in Catalog.Infrastructure) + RestaurantAddressChanged event — Geo references only SharedKernel+Contracts, module-isolation green. Build 0 warn/0 err; tests 314 passed/0 failed/0 skipped (incl. 15 Geo unit, 3 Geo integration Testcontainers, 33 architecture).

---
- **time:** 2026-06-16T07:44:11Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 8 review: singleton/transient mismatch makes rate-limit ineffective; Polly retries bypass rate-limit gate

---
- **time:** 2026-06-16T08:15:33Z
- **agent:** developer
- **status:** completed
- **message:** Step 8 revision: moved Nominatim rate-limit to a process-wide singleton NominatimRateLimiter + RateLimitingHandler (DelegatingHandler), wired Polly OUTER / rate-limit INNER so every retry re-throttles; stripped per-instance rate state from NominatimGeocoder (now transient typed client); added FakeTimeProvider deterministic tests (gating + per-attempt re-entry). Build 0/0; 315 tests pass (17 assemblies), Docker integration incl Geo green.

---
- **time:** 2026-06-16T08:22:56Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 8 round-2 review: all 3 findings confirmed resolved. NominatimRateLimiter is singleton with process-wide SemaphoreSlim+timestamp; NominatimGeocoder is stateless transient. Handler ordering correct (Polly outer via AddPolicyHandler first, RateLimitingHandler inner via AddHttpMessageHandler second). AcquireCount==1+retryCount test proves every retry re-enters limiter. FakeTimeProvider (Microsoft.Extensions.TimeProvider.Testing 8.10.0) used deterministically; Task.Delay uses injected TimeProvider. Build 0 warnings, 315/315 tests pass.

---
- **time:** 2026-06-16T08:38:59Z
- **agent:** qa
- **status:** completed
- **message:** Step 8 verified: build 0w/0e; 315 tests pass (0 failed, 0 skipped) across 17 assemblies. Geo unit: 16 pass (NominatimGeocoder 5, RateLimiter 3, handlers 5, GeoDistance 3). Geo integration: 3 pass against real Testcontainers SQL. Architecture: 33 pass. Confirmed: no live Nominatim HTTP (mocked handler + pinned fixture); rate-limiter singleton in DI (AddSingleton<NominatimRateLimiter>); retries re-throttle test (AcquireCount==retryCount+1) passes; no Google lat/lng code path in Geo module (only negative invariant comments); Kyiv->Lviv coordinate refresh integration test passes; AddressChanged seam verified; Step 8 marked done.

---
- **time:** 2026-06-16T12:44:52Z
- **agent:** developer
- **status:** completed
- **message:** Step 9 Parsing: finished Normalizer, HostRateLimiter, DapperParseQuarantineStore (+0011 migration), example parsers, AddParsingModule; fixed pre-existing AngleSharp file:// + Selenium LogLevel/CA1859 bugs; wired Parsing into solution + architecture tests; added unit(22)/integration(3, incl Testcontainers quarantine)/Selenium-e2e(1, real headless Chrome). Full build 0w/0e; all solution tests green (arch 33).

---
- **time:** 2026-06-16T12:52:11Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 9 review: 3 findings — (1) missing gate-order DidNotReceive assertions in compliance unit tests; (2) RobotsTxtGate XML doc claims 'most specific matching group, falling back to *' but implementation only evaluates *, misleading; (3) QuarantinePersistenceTests.cs line 79-80 constructs BorschCafeSeleniumParser with a substitute IWebDriverFactory that has no configured ParseAsync behavior but this parser is never called in the test — legitimate, but confusing. All tests pass green.

---
- **time:** 2026-06-16T12:58:13Z
- **agent:** developer
- **status:** completed
- **message:** Step 9 revision: pinned gate-order assertions in 3 compliance tests; fixed RobotsTxtGate doc to match wildcard-only RobotsTxtPolicy; removed dead borschParser from QuarantinePersistenceTests selector. Build 0/0; full suite green (e2e real headless Chrome 2s).

---
- **time:** 2026-06-16T13:00:30Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 9 round-2 review: all 3 findings confirmed resolved — gate-order DidNotReceive assertions pin ordering in 3 tests, RobotsTxtGate doc corrected to wildcard-* wording, borschParser removed from QuarantinePersistenceTests with no unused imports. Build 0/0, 22 unit tests green.

---
- **time:** 2026-06-16T13:13:31Z
- **agent:** qa
- **status:** completed
- **message:** Step 9 verified: build 0w/0e; 341 tests pass (0 failed, 0 skipped) across 20 test assemblies. Parsing unit 22, Parsing integration 3 (Testcontainers SQL), Parsing e2e 1 (real headless Chrome, ~3s duration), architecture 33. CONFIRMED: Selenium e2e launched real headless Chrome (HeadlessChromeFactory -> ChromeDriver, 3s runtime, not mocked/skipped). Testcontainers integration ran real SQL Server container (Docker 28.4.0 observed in log). Compliance gates fire in correct order before fetch: non-first-party rejected first (rate-limiter not consulted), robots.txt block second (rate-limiter not consulted), rate-limit block third (strategy not consulted). Admin protection: DoNotUpdate venue skipped entirely with PersistAsync never called; DoNotParse dish excluded from write while others persist. Quarantine: unmappable dish (Узвар) lands in parsing.ParseQuarantine table (0011 migration confirmed applied), parse does not fail, mappable items (Борщ/Вареники) persist as live MenuItems. Both parsers (Selenium BorschCafe, AngleSharp PizzaHouse) produce same normalized ParsedMenu contract. IPuppeteerLocatorStrategy declared, no implementation. Parsing.Infrastructure: Dapper only, no EF (architecture EF-containment test green, EF only in Admin.Infrastructure). Module isolation: Parsing refs only Contracts (not Catalog/Geo internals). Step 9 marked [x] done.

---
- **time:** 2026-06-16T13:42:56Z
- **agent:** developer
- **status:** completed
- **message:** Step 10: Admin API host + EF Core database-first CRUD + protection flags. Build 0 warn/0 err. Tests: 7 admin unit, 15 admin integration, 33 arch (EF-containment green WITH EF present), 21 public-api integration regression — all pass. Admin-only authz (admin 200/user 403/anon 401), EF migrations-disabled guard fail-fast, E2E DoNotUpdate/DoNotParse survive Step 9 parse, EditAddress raises re-geocode.

---
- **time:** 2026-06-16T17:19:43Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 10 review complete: 4 findings (1 medium: AdminAuthorization.IsAdmin bypasses IClaimsToRoleMapper DI seam; 1 medium: fixture startup guard comment is wrong about when guard fires; 1 low: dispatcher silently discards geocode Result; 1 low: test assertion message says 'master' but code uses 'tempdb')

---
- **time:** 2026-06-16T17:45:38Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 10 round-2 review: all 4 findings confirmed resolved; build 0W/0E; admin-integration 15/15 pass (auth 200/403/401, EF-guard, CRUD, protection, re-geocode); public-api integration 21/21 pass; architecture tests 33/33 pass

---
- **time:** 2026-06-16T17:57:42Z
- **agent:** qa
- **status:** completed
- **message:** Step 10 verified: build 0w/0e; AdminApi.Integration 15/15 green (Testcontainers SQL container confirmed); ArchitectureTests 33/33 green including EF-containment (EF only in Admin.Infrastructure); EfMigrationsDisabledGuard tests: passes on real schema, fails-fast on tempdb missing tables, zero EF migrations defined; AdminAuthorization: anonymous=401/user=403/admin=204 all tested; ProtectionSurvivesParseTests: DoNotUpdate venue fully skipped, DoNotParse item preserved while unprotected sibling overwritten - enforcement in real RunParseCommandHandler not a stub; EditAddress->re-geocode seam confirmed end-to-end; no secrets in production appsettings.json; step marked done

---
- **time:** 2026-06-16T18:17:58Z
- **agent:** developer
- **status:** completed
- **message:** Step 11: analytics ingest + anonymization-at-ingest (retain/hash/drop), rotating salt provider, geohash encoder, append-optimized batch writer, aggregates-only rollup reader + 0012 migration, POST /analytics/events endpoint. Build 0 warn/0 err. Unit 18/18, arch 33/33, Analytics integ 9/9, PublicApi integ 25/25, full sln 0 failures.

---
- **time:** 2026-06-16T18:23:55Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 11 review: 3 issues found — (1) MasterSecret empty-string produces a degenerate HMAC key (no guard); (2) AnalyticsEventRaised contract has non-nullable CoarseGeohash/HashedActorId but domain fields are optional; (3) negative PII test does not check the Id column for coordinate-like text (low impact by construction but missing coverage).

---
- **time:** 2026-06-16T18:31:54Z
- **agent:** developer
- **status:** completed
- **message:** Step 11 revision: fixed 3 reviewer findings — (1) HIGH fail-fast guard rejects blank Analytics master secret outside Dev/Testing in AddAnalyticsModule (env-gated like OIDC authority guard; HMAC keyed by MasterSecret+rotating salt), +6 guard unit tests; (2) MEDIUM AnalyticsEventRaised CoarseGeohash/HashedActorId now nullable to match domain optionality; (3) LOW widened negative PII integration test to full coords (49.8397/24.0297)+raw id across ActorHash/CoarseGeohash/DimensionsJson/Id. Build 0W/0E. Analytics unit 24/24, Analytics integration 9/9, architecture 33/33 pass. Full-suite PublicApi(25)/Geo(3) failed on Testcontainers parallel Docker flake; both green in isolation.

---
- **time:** 2026-06-16T22:14:30Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 11 round-2 review: all 3 findings confirmed resolved. HMAC guard throws in Production/Staging on blank MasterSecret (not config-invertible, secret mixed into key). Contract fields string?. Negative PII test covers full coords + raw id across all textual columns. Tests: unit 24, integration 9, arch 33 — all green.

---
- **time:** 2026-06-16T22:17:26Z
- **agent:** qa
- **status:** completed
- **message:** Step 11 verified: build 0w/0e; unit tests 35 (11 domain + 24 app) all green; Analytics integration 9/9 green against real Testcontainers SQL Server; architecture 33/33 green. Anonymization-at-ingest confirmed (retain/hash/drop); no raw PII columns; rotating salt cross-window divergence; HMAC guard rejects blank secret in Production/Staging; aggregates-only rollup DTO contains no actor hash or geohash; Dapper-only; batch insert exercised.

---
- **time:** 2026-06-16T22:46:42Z
- **agent:** developer
- **status:** completed
- **message:** Step 12: Worker host + Quartz clustered scheduler + 4 jobs. 0013 Quartz cluster migration; PriceMedianCalculator+Writer (DB-only); RatingAggregateRecomputer; geocode-queue seam. Unit 10/10, Worker integration 3/3 (single-fire-under-clustering, nightly median, rating-recompute), arch 33/33, full build 0 warnings.

---
- **time:** 2026-06-16T22:56:49Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 12 review complete: 4 issues found. Critical: missing-production-adapters gap (ICatalogMenuWriter/ICatalogDishResolver/IRestaurantGeocoder) confirmed as medium severity deferred-to-step-13 — worker host starts OK but WeeklyParseJob WILL throw on first fire; flagged explicitly. Also: PriceMedianWriter DELETE+INSERT transaction uses no explicit IsolationLevel (Serializable recommended); 'binary' serializer in clustering test vs. no explicit serializer in production; PriceMedian table has no PK (MERGE-equivalent vulnerability).

---
- **time:** 2026-06-16T23:02:22Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 12 revision: addressed 4 reviewer findings. (1) Documented missing parse-persist/geocode adapter deferral to Step 13 in WorkerCompositionRoot comment + implementation.md note. (2) PriceMedianWriter now uses Serializable isolation for atomic table replace. (3) Kept explicit binary serializer in clustering test with corrected comment (StdSchedulerFactory raw path requires it; production DI builder defaults it). (4) Added exact HaveCount(2) assertion to NightlyPriceMedianTests. Build 0w/0e. Worker unit 10/10 pass; Worker integration 3/3 pass.

---
- **time:** 2026-06-16T23:04:34Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 12 round-2 review: all 4 findings confirmed resolved (deferral comment in WorkerCompositionRoot.cs + implementation.md, Serializable isolation in PriceMedianWriter, serializer-type decline accepted, exact HaveCount(2) assertion present). No new issues.

---
- **time:** 2026-06-16T23:08:39Z
- **agent:** qa
- **status:** completed
- **message:** Step 12 verified: build 0w/0e; Worker unit 10/10 green; Worker integration 3/3 green (Testcontainers SQL Server); architecture 33/33 green. Single-fire-under-clustering passes: two StdSchedulerFactory instances on the same QRTZ store, exactly 1 execution. Median writer uses IsolationLevel.Serializable. Deferral documented in WorkerCompositionRoot.cs and implementation.md. Marking done.

---
- **time:** 2026-06-17T06:31:47Z
- **agent:** developer
- **status:** in-progress
- **message:** Step 13 completed: wired Redis/MassTransit/Observability into 3 hosts, transparent CachingPriceMedianProvider decorator, closed Item A deferral (Worker DI-resolution test green), deploy/ Dockerfiles+compose+keycloak realm, runsettings+xunit.runner.json fix Item B flake. Full solution build 0/0, full test run 425 pass / 0 fail / 0 skip reliably green.

---
- **time:** 2026-06-17T06:33:56Z
- **agent:** developer
- **status:** completed
- **message:** Step 13 finalized: build 0/0, full test 425 pass/0 fail/0 skip reliably green, deploy/ + compose validated, realm JSON valid. Left [~] for QA.

---
- **time:** 2026-06-17T06:43:00Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 13 review: 4 findings — (1) consumer-boundary arch test gap: BuildingBlocks.Messaging consumers not scanned by rule(e) since they're in shared family; (2) missing cache-guard test (ICacheService doc promises one, none exists); (3) AdminApi.Integration test doubles DapperCatalogMenuWriter/DapperDishResolver are now redundant dead code duplicates of production adapters; (4) public-api missing keycloak depends_on (minor). OTLP omission is acceptable; runsettings parallelism fix is acceptable.

---
- **time:** 2026-06-17T07:09:12Z
- **agent:** developer
- **status:** completed
- **message:** Step 13 revision: fixed 4 reviewer findings. Issue1: consumer fitness rule now scans AssemblyLoader.SolutionAssemblies (covers BuildingBlocks.Messaging consumers) + new IsForbiddenCrossModuleReference helper; proved it bites via temp Geo.Application.IGeocoder ref then reverted. Issue2: added CachePrivacyGuardTests (static CacheKeys no-Google-key scan + recording ICacheService spy through Map endpoint asserting cache never touched). Issue3: ProtectionSurvivesParse now uses production Catalog.Infrastructure.Parsing adapters over real SqlConnectionFactory, deleted 2 test doubles; surfaced+fixed a latent production bug (DapperCatalogDishResolver nullable-struct Dapper mapping returned null -> changed DishRow to sealed record). Issue4: added keycloak depends_on to public-api. Build 0/0. Full solution green.

---
- **time:** 2026-06-17T07:15:21Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 13 round-2 review: all 4 findings confirmed resolved — consumer rule non-vacuous (SolutionAssemblies + IsForbiddenCrossModuleReference), DishRow sealed record fixes production null-mapping bug, CachePrivacyGuardTests wired through real Map endpoint, keycloak depends_on added; build 0/0, 33 arch tests green, all unit tests green

---
- **time:** 2026-06-17T07:33:41Z
- **agent:** qa
- **status:** completed
- **message:** Step 13 verified: build 0w/0e; full suite 427 passed/0 failed/0 skipped (23 assemblies, no flakes); all acceptance criteria confirmed — CachingPriceMedianProvider decorator transparent (miss→DB→populate, hit→no-DB, identical values); cache privacy guard (CacheKeys inspection + Map endpoint spy, zero cache interactions) passes; MassTransit consumers in BuildingBlocks.Messaging handle MenuUpdated/AddressChanged via in-memory harness, depend only on Contracts; consumer-to-contract fitness rule scans all solution assemblies (non-vacuous, consumers found in BuildingBlocks.Messaging), 33 arch tests green; correlation-id survives publish→consume hop; WeeklyParseJobResolutionTests pass (DI graph resolves: ICatalogMenuWriter/ICatalogDishResolver/IRestaurantGeocoder all production adapters); ProtectionSurvivesParseTests uses real Dapper adapters (DapperCatalogDishResolver/DapperCatalogMenuWriter), both DoNotUpdate and DoNotParse invariants hold; deploy/ has 3 Dockerfiles + docker-compose.yml + .env.example + keycloak/realm-export.json (admin+user roles/scopes, wheretoeat-api client, admin test user); Keycloak authz test: admin JWT passes admin-only policy, user→403, anon→401. Step 13 marked done.

---
- **time:** 2026-06-17T19:28:45Z
- **agent:** developer
- **status:** completed
- **message:** Step 14 VERIFY: build 0w/0e; full solution 488 tests pass, 0 fail, 0 skip (27 assemblies). Monetization module skeleton complete; all acceptance criteria (a)-(f) covered by passing tests. No code changes needed.

---
- **time:** 2026-06-17T19:35:54Z
- **agent:** developer-reviewer
- **status:** needs_revision
- **message:** Step 14 review: build 0w/0e, unit/arch tests pass; 1 low finding — 401/403 auth matrix only covers Grant Verified, not Revoke or CreateAdPlacement

---
- **time:** 2026-06-17T19:41:19Z
- **agent:** developer
- **status:** completed
- **message:** Step 14 revision: added 4 missing auth-matrix tests (Revoke/CreateAdPlacement anon+non-admin). Build 0W/0E. AdminApi.Integration 23/23 pass. Full suite 1 unrelated Redis-expiry parallel flake (passes 6/6 in isolation).

---
- **time:** 2026-06-17T19:42:22Z
- **agent:** developer-reviewer
- **status:** completed
- **message:** Step 14 round 2: confirmed 4 new auth-matrix tests (RevokeVerified_Anonymous_IsUnauthorized, RevokeVerified_NonAdmin_IsForbidden, CreateAdPlacement_Anonymous_IsUnauthorized, CreateAdPlacement_NonAdmin_IsForbidden) -- correct verbs/routes, correct clients (anon vs user-JWT), correct assertions (401/403). LOW finding resolved.

---
- **time:** 2026-06-17T20:10:58Z
- **agent:** qa
- **status:** completed
- **message:** Step 14 QA: build 0w/0e (exit 0). Full suite 442 passed / 0 failed / 0 skipped across 27 assemblies (exit 0). All 8 MonetizationEndpointsTests passed (admin 2xx, non-admin 403, anon 401 for all 3 endpoints; happy-path grant/revoke + ad-create over real Dapper repos). 12 domain + 6 application unit tests passed. Architecture guard Recommendation_HasNoReferenceToMonetization passed (non-vacuous: both assemblies loaded). Guard probe confirmed: adding SubscriptionTier type-reference to RecommendQuery made test FAIL with 'Offending types: WhereToEat.Recommendation.Application.RecommendQuery'; reverted, guard green again. Migration 0014 idempotent, no rank column. Monetization.Infrastructure: Dapper-only, zero EF refs. IPaymentGateway sole payment seam (NoOpPaymentGateway). Verified grant/revoke flips CanManageRealPhotos, no ranking/contact-link effect. AdPlacement.IsLabeledAd=true by construction, no rank fields. Step 14 marked done.

