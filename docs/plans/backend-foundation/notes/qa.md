# QA Notes — backend-foundation

## Environment Readiness Check (pre-Step-1, 2026-06-14)

### .NET SDK
- SDKs installed: 6.0.428, **8.0.417**, 9.0.304
- .NET 8 LTS SDK present — plan's target TFM satisfied
- ASP.NET Core 8.0 runtime installed (8.0.19, 8.0.23)
- `dotnet test` can run; xUnit template available

### NuGet Package Cache (C:\Users\Viktor\.nuget\packages\)
PRESENT (will not need download for Step 1-7 work):
- xunit, xunit.runner.visualstudio, xunit.abstractions (v2.4–2.9)
- fluentassertions 6.0.0
- microsoft.entityframeworkcore.sqlserver (for Step 10)
- microsoft.data.sqlclient
- dbup, dbup-core, dbup-sqlserver 5.0.8
- masstransit 8.5.4, masstransit.rabbitmq
- quartz 3.15.1, quartz.extensions.*
- serilog, serilog.aspnetcore + sinks
- opentelemetry + instrumentation packages
- selenium.webdriver 4.16.2 / 4.35.0
- selenium.webdriver.chromedriver 120.x / 140.x (mismatch with Chrome 149 — see below)
- microsoft.aspnetcore.mvc.testing
- microsoft.extensions.http (7.0, 8.0)
- polly 7.2.3
- stackexchange.redis
- microsoft.extensions.caching.stackexchangeredis

NOT IN CACHE (will be downloaded on first `dotnet restore`):
- NSubstitute (plan's mock library — Step 1 unit tests)
- Testcontainers for .NET (SQL Server container — Step 4+)
- NetArchTest.Rules (architecture fitness tests — Step 2)
- AngleSharp (library-parser strategy — Step 9)
- Dapper (hot-path persistence — Step 4)
  Note: Dapper is not in local cache but will download from NuGet.org normally

### Docker / Testcontainers
- Docker Desktop 28.4.0 INSTALLED
- Docker daemon: NOT RUNNING at check time (both named pipes closed; Docker Desktop process absent from task list)
- docker-compose 2.39.4 available via Docker Desktop
- Impact: Testcontainers (Steps 4, 5, 6, 7, 8, 9, 10, 11, 12, 13) cannot run until Docker Desktop is started
- EULA for SQL Server container (mcr.microsoft.com/mssql/server): must accept `ACCEPT_EULA=Y` env var when pulling — standard Testcontainers pattern, not a blocker once Docker is up

### SQL Server
- No local SQL Server instance detected on port 1433
- Will be provided exclusively via Testcontainers SQL Server image
- No port conflicts detected on 1433, 5672, 6379, 8080, 15672

### Selenium / E2E (Step 9)
- Chrome 149.0.7827.103 installed at C:\Program Files\Google\Chrome\Application\
- Edge 149.0.4022.69 installed at C:\Program Files (x86)\Microsoft\Edge\Application\
- chromedriver NOT on PATH; no explicit driver binary present
- Selenium.WebDriver 4.35.0 includes **Selenium Manager** (selenium-manager.exe in win/native) — will auto-download matching ChromeDriver for Chrome 149 on first run, provided internet access is available
- Cached selenium.webdriver.chromedriver NuGet packages are v120/v140 — these do NOT match Chrome 149; the plan must use Selenium Manager auto-resolution or pin the NuGet chromedriver package to a 149.x build. Recommend using Selenium Manager (Selenium 4.6+ built-in, present in 4.35.0).
- Edge available as fallback headless driver if needed

### Redis / RabbitMQ / Keycloak (Step 13)
- All will be provided via docker-compose — no local instances needed
- stackexchange.redis cached; masstransit.rabbitmq cached

### Port Environment
- No conflicts detected on key ports (1433, 5672, 6379, 8080, 15672)

### Summary Classification

| Tool / Package | Status | Needed from Step | Blocker? |
|---|---|---|---|
| .NET 8 SDK | PRESENT (8.0.417) | Step 1 | N/A |
| xUnit | PRESENT in cache | Step 1 | No |
| FluentAssertions 6.x | PRESENT in cache | Step 1 | No |
| NSubstitute | NOT IN CACHE | Step 1 | No (downloads on restore) |
| NetArchTest.Rules | NOT IN CACHE | Step 2 | No (downloads on restore) |
| Testcontainers .NET | NOT IN CACHE | Step 4 | No (downloads on restore) |
| Docker daemon | NOT RUNNING | Step 4 | Medium — must start Docker Desktop before Step 4 integration tests |
| Dapper | NOT IN CACHE | Step 4 | No (downloads on restore) |
| DbUp/DbUp-SqlServer | PRESENT in cache | Step 4 | No |
| AngleSharp | NOT IN CACHE | Step 9 | No (downloads on restore) |
| Selenium 4.35 + Manager | PRESENT in cache | Step 9 | No — Manager auto-downloads driver |
| ChromeDriver NuGet | v120/v140 cached (MISMATCH v149) | Step 9 | Low — Selenium Manager resolves this |
| Chrome 149 browser | PRESENT | Step 9 | No |
| MassTransit | PRESENT in cache | Step 13 | No |
| Quartz.NET | PRESENT in cache | Step 12 | No |
| Redis (StackExchange) | PRESENT in cache | Step 13 | No |
| Docker Compose | PRESENT | Step 13 | Needs daemon running |

### Recommended developer action before Step 4
Start Docker Desktop before running integration tests (Step 4+). All other missing packages will resolve via `dotnet restore`.

## Step 14 QA Findings (2026-06-17)

### Verification Summary
All Step 14 acceptance criteria verified against real behavior. Step marked [x] done.

### Build
- `dotnet build src/WhereToEat.sln --no-incremental`: exit 0, **0 warnings, 0 errors**.

### Test Counts (all green — 442 passed / 0 failed / 0 skipped across 27 assemblies, exit 0)
- Monetization.Domain.UnitTests: **12/12** — LabeledAdInvariantTests (5), VerifiedStatusTests (7)
- Monetization.Application.UnitTests: **6/6** — PaymentSeamTests covering NoOp gateway, grant/revoke/create with payment accepted/rejected
- AdminApi.Integration: **23/23** — 8 MonetizationEndpointsTests (admin 2xx, non-admin 403, anon 401 full matrix; GrantThenRevoke + CreateAdPlacement happy paths) + existing 15
- ArchitectureTests: **34/34** — now 34 with `Recommendation_HasNoReferenceToMonetization` added
- All other assemblies: unchanged and green. No flakes observed.

### Guard Test Probe (introduce-then-revert)
- Added `SubscriptionTier` type reference (via static field) to `RecommendQuery.cs` plus a project reference from `Recommendation.Application` to `Monetization.Domain`.
- Build: exit 0 (type is actually referenced via field).
- Architecture test result: **FAIL** with message: `WhereToEat.Recommendation.* must have NO reference to WhereToEat.Monetization.* — organic ranking is not for sale (invariant #10). Offending types: WhereToEat.Recommendation.Application.RecommendQuery`.
- Both `recommendationAssemblies` and `monetizationNamespaces` were non-empty before the assertion (non-vacuous).
- All changes reverted; build returns to 0w/0e; guard passes again.
- Note: a project reference alone (without type usage in IL) does NOT trigger the guard — NetArchTest checks IL dependencies, not project references.

### Acceptance Criteria Results

**Verified grant/revoke flips photo gate, no ranking effect:**
- `GrantThenRevokeVerified_AsAdmin_TogglesTheStoredTier`: admin grants Pro → DB tier = 2; admin revokes → tier = 0. Both 204.
- `VerifiedStatusTests.Grant_PaidTier_UnlocksRealPhotos`: `CanManageRealPhotos` true after grant, false after revoke. `IsVerified` flips accordingly.
- `PaymentSeamTests.GrantVerified_RunsThroughThePaymentSeam_AndFlipsThePhotoGateAndTier`: persisted `VerifiedStatus.Tier = Pro`, `CanManageRealPhotos = true`. No ranking field on `VerifiedStatus`.
- Contact links always free (§5.8): `VerifiedStatus` carries no contact-link-gating field; endpoints expose no contact-link toggle.

**AdPlacement always labeled, no organic score path:**
- `LabeledAdInvariantTests.AdPlacement_IsAlwaysLabeled`: `IsLabeledAd = true` on every created instance.
- `LabeledAdInvariantTests.PaidPlacement_HasNoPublicFieldThatCouldFeedOrganicRanking`: reflection scan over `AdPlacement` and `Promotion` public members for tokens `rank/boost/weight/priority/score/relevance/organic/position/sortorder` → empty list. Test green.
- Guard test above confirms Recommendation has zero IL dependency on Monetization.

**IPaymentGateway sole payment seam:**
- `IPaymentGateway` in Application; only impl is `NoOpPaymentGateway` (Infrastructure).
- `PaymentSeamTests.GrantVerified_WhenPaymentRejected_DoesNotPersist`: rejecting gateway → `IsFailure = true`; `UpsertAsync` never called.
- `PaymentSeamTests.CreateAdPlacement_WhenPaymentRejected_DoesNotPersist`: same pattern; `AddAsync` never called.
- No EF in Monetization (grep confirmed zero EF references across all Monetization projects).

**Admin endpoints authorized by Step 10 AdminAuthorization policy:**
- All 3 endpoints (GrantVerified PUT, RevokeVerified DELETE, CreateAdPlacement POST) carry `.RequireAuthorization(AdminAuthorization.PolicyName)`.
- Full 3×3 matrix tested: anonymous → 401, non-admin user → 403, admin → 2xx (all 8 tests pass).

**Migration 0014 idempotent, no rank column:**
- `IF NOT EXISTS` guard on `IX_AdPlacement_TargetingKey_Window` — idempotent.
- Applies atop 0008 (which created the monetization schema/tables).
- Comment explicitly states no rank/boost/weight/priority/score column exists or is added.

**Monetization.Infrastructure uses Dapper only (no EF):**
- Zero EF references in any Monetization project confirmed via grep.
- `AddMonetizationModule` registers only `ISqlConnectionFactory`, Dapper repos, handlers, and the no-op gateway.
- EF-containment architecture test still 1/1 green.

## Step 13 QA Findings (2026-06-17)

### Verification Summary
All Step 13 acceptance criteria verified against real behavior. Step marked [x] done.

### Build
- `dotnet build src/WhereToEat.sln`: exit 0, **0 warnings, 0 errors**.

### Test Counts (all green — 427 passed / 0 failed / 0 skipped across 23 assemblies)
- CrossCutting.Integration: **16/16** — RedisCacheServiceTests(6), CachingPriceMedianProviderTests(1), MessagingConsumerTests(2), CorrelationIdPropagationTests(1), KeycloakRealmAndAdminPolicyTests(5), plus others
- Worker.Integration: **4/4** (WeeklyParseJobResolutionTests included)
- AdminApi.Integration: **15/15** (ProtectionSurvivesParseTests included)
- ArchitectureTests: **33/33** including consumer-to-contract (non-vacuous — scans BuildingBlocks.Messaging)
- All other assemblies: unchanged and green. No flakes observed in this run.

### Acceptance Criteria Results

**CachingPriceMedianProvider decorator transparency:**
- Miss: reads Step 7 DB median table, populates cache, returns same value as DB-only provider.
- Hit: DB row deleted after first call; decorator still returns cached value (no DB read).
- Identical return values vs DB-only provider confirmed in `CachingPriceMedianProviderTests.Miss_reads_db_and_populates_then_hit_skips_db_with_identical_value`.

**Redis ICacheService (get/set/remove/expiry):**
- `RedisCacheServiceTests`: Set/Get, Get-on-miss→null, Remove deletes, Set-with-expiry lapses after 400ms wait, RemoveByPrefix scoped deletion, TryClaimSlot grants once then refuses. All 6 tests pass against Testcontainers Redis.

**Cache privacy guard (no Google rating/coord cached):**
- `CacheKeys_DefineNoGoogleSourcedKey`: all const and computed CacheKeys strings inspected via reflection; none contain "google", "gmaps", "googlerating", "google-rating".
- `MapEndpoint_NeverTouchesTheCache`: `RecordingCacheService` spy wired via `ConfigureTestServices`; Map endpoint responded 200; `spy.Interactions` empty — cache never touched.

**MassTransit consumers (in-memory harness, Contracts-only):**
- `GeocodeOnAddressChangedConsumer` handles `RestaurantAddressChanged` → calls `IRestaurantGeocoder` (Contracts seam); references only Contracts.
- `InvalidateCacheOnMenuUpdatedConsumer` handles `MenuUpdated` → calls `ICacheService.RemoveByPrefixAsync(PriceMedianPrefix)` + `RemoveAsync(CategoryList)`.
- Both consumers live in `BuildingBlocks.Messaging`; architecture consumer-boundary test non-vacuous (scans ALL solution assemblies).

**Consumer-to-contract fitness rule (non-vacuous):**
- `MessageConsumers_ReferenceOnlyContractsAcrossModuleLines` passes. Scan covers BuildingBlocks.Messaging (where the Step 13 consumers live, resolving to module=null). Confirmed both consumers detected and verified.

**Correlation ID propagation:**
- `Correlation_id_survives_a_publish_then_consume_hop`: publisher sets baggage `correlation-id=corr-7f3a9b2e` on an ActivitySource activity; MassTransit in-memory harness carries baggage to consumer; consumer reads same id from `Activity.Current.GetBaggageItem`. Verified via `CorrelationIdPropagationTests`.

**WeeklyParseJob DI resolution (Step 12 deferral closed):**
- `WeeklyParseJobResolutionTests.WeeklyParseJob_full_dependency_graph_resolves`: `AddJobDependencies` registers production `ICatalogMenuWriter` (`DapperCatalogMenuWriter`), `ICatalogDishResolver` (`DapperCatalogDishResolver`), `IRestaurantGeocoder` (Geo.Infrastructure adapter). `BuildServiceProvider(validateOnBuild: true)` + `GetRequiredService<WeeklyParseJob>()` succeeds without exception. All three previously-deferred seams confirmed as real adapters.

**Production-adapter protection test:**
- `ProtectionSurvivesParseTests` uses `DapperCatalogDishResolver` and `DapperCatalogMenuWriter` (real production adapters over the fixture DB — no test doubles for these seams).
- `DoNotUpdate_RestaurantIsSkippedEntirely_ByTheParse`: outcome `SkippedDoNotUpdate`; item count unchanged; curated price 95 UAH untouched; no new item added.
- `DoNotParse_ItemSurvives_WhileUnprotectedItemIsOverwritten`: protected item stays at 200 UAH; open item updated to 85 UAH. Invariant #3 end-to-end verified.

**Deploy artifacts:**
- `deploy/Dockerfile.PublicApi`, `deploy/Dockerfile.AdminApi`, `deploy/Dockerfile.Worker`: all present, multi-stage SDK→runtime builds, correct ENTRYPOINT.
- `deploy/docker-compose.yml`: SQL Server, Redis, RabbitMQ, Keycloak + 3 hosts + one-shot migrations service. Both `public-api` and `admin-api` have `depends_on: keycloak`. Worker depends on migrations+redis+rabbitmq.
- `deploy/.env.example`: replica knobs (`PUBLIC_API_REPLICAS`, `ADMIN_API_REPLICAS`, `WORKER_REPLICAS`) and CPU/mem limits as env vars; MESSAGING_TRANSPORT switchable (`InMemory` vs `RabbitMq`).
- `deploy/keycloak/realm-export.json`: defines `admin` and `user` realm roles, `admin` and `user` client scopes, `wheretoeat-api` client with role→claim mapper, admin test user `admin-test` with `realmRoles: ["admin", "user"]`.

**Keycloak realm/authz test:**
- `Realm_export_defines_admin_and_user_roles`: both roles present in JSON. Pass.
- `Realm_export_defines_admin_and_user_client_scopes`: both scopes present. Pass.
- `Realm_export_defines_the_api_client_and_an_admin_test_user`: `wheretoeat-api` client found; admin test user with `admin` realm role found. Pass.
- `Admin_role_token_satisfies_the_admin_only_policy`: `AdminAuthorization` policy accepts `role=admin` claim. `IClaimsToRoleMapper` maps to `UserRole.Admin`. Pass.
- `User_role_token_is_denied_by_the_admin_only_policy`: `role=user` claim → policy fails. Pass.
- `Anonymous_principal_is_denied_by_the_admin_only_policy`: unauthenticated `ClaimsPrincipal` → policy fails. Pass.

## Step 12 QA Findings (2026-06-17)

### Verification Summary
All Step 12 acceptance criteria verified against real behavior. Step marked [x] done.

### Build
- `dotnet build src\WhereToEat.sln --no-incremental`: exit 0, **0 warnings, 0 errors**.

### Test Counts (all green)
- Worker unit tests (`WhereToEat.Worker.UnitTests`): **10/10** — PriceMedianCalculatorTests (7) + JobScheduleTests (3).
- Worker integration tests (`WhereToEat.Worker.Integration`, run in isolation): **3/3** — `SingleFireUnderClusteringTests`, `NightlyPriceMedianTests`, `RatingRecomputeTests`. Real Testcontainers SQL Server 2022 container confirmed (Docker 28.4.0, WSL2 kernel).
- Architecture tests (`WhereToEat.ArchitectureTests`): **33/33** unchanged.

### Acceptance Criteria Results

**Clustered single-fire (the key clustering proof):**
- `SingleFireUnderClusteringTests.A_scheduled_trigger_fires_on_exactly_one_clustered_instance` passed: two independent `StdSchedulerFactory` scheduler instances (`instance-A`, `instance-B`) sharing scheduler name `WhereToEatWorkerScheduler` on the same Testcontainers SQL Server. Job scheduled once (one-shot SimpleTrigger), `CountingJob.ExecutionCount` polled to 1, then a 5-second settle window confirmed no second fire. Final count = 1. This is genuine two-instance clustering over the QRTZ_* tables from migration 0013.

**Quartz clustered store (migration 0013):**
- `0013_quartz_cluster_tables.sql` present: all QRTZ_* tables (QRTZ_JOB_DETAILS, QRTZ_TRIGGERS, QRTZ_FIRED_TRIGGERS, QRTZ_SCHEDULER_STATE, QRTZ_LOCKS, etc.) created idempotently (IF NOT EXISTS guards). `QRTZ_SCHEDULER_STATE` and `QRTZ_FIRED_TRIGGERS` are the cluster election tables.
- `WorkerCompositionRoot.AddClusteredQuartz`: `store.UseClustering()` with 10s checkin interval, 20s misfire threshold. `quartz.MisfireThreshold = 60s`. `store.UseProperties = true`.
- All four jobs declared `[DisallowConcurrentExecution]`.
- `JobSchedule.AddCronJob` calls `.RequestRecovery()` and `.StoreDurably()` on every job.

**Nightly median (DB-only):**
- `NightlyPriceMedianTests.Writes_per_area_and_city_wide_rows_that_the_db_provider_reads_back` passed: 5 items in one grid cell (prices 100/110/120/130/140) + 1 item in a sparse cell (500). Writer uses `areaSampleThreshold=5`.
  - Dense area: per-area median = 120, sampleSize = 5.
  - City-wide fallback: (120+130)/2 = 125, sampleSize = 6.
  - Sparse area (below N): no per-area row; DB provider returns 125 (city-wide fallback).
  - Exact count assertion: `dishRows.Should().HaveCount(2)` — exactly one city-wide row and one per-area row, no stray rows for the sparse area.
  - `DbPriceMedianProvider` reads exactly the same values the writer put in — the Step 7 read/Step 12 write handoff is end-to-end correct on real DB.
- No Redis involved (confirmed by `PriceMedianWriter.cs` — DB-only, `ISqlConnectionFactory` only dependency).

**Median replace isolation (Serializable):**
- `PriceMedianWriter.RecomputeAsync` uses `IsolationLevel.Serializable` for the DELETE+INSERT transaction, documented with an explicit comment in the source explaining the rationale (RCSI gap prevention). Code confirmed at `PriceMedianWriter.cs` lines 63–64.

**Rating recompute:**
- `RatingRecomputeTests.Materialized_aggregate_matches_the_shared_kernel_bayesian_formula` passed: 6 raw scores (5,4,5,3,4,5) → `DapperRatingAggregateRecomputer.RecomputeAllAsync` writes sum=26, count=6 aggregate. `BayesianRatingSmoothing.Smooth` over materialized equals `BayesianRatingSmoothing.Smooth` over raw. Sparse venue (single 5-star) is smoothed below 5 and above DefaultGlobalMean. `written == 2` (two restaurants materialized).

**Weekly parse delegates to Step 9 (admin protection enforced there):**
- `WeeklyParseJob.Execute` iterates sources from `IParseSourceProvider` and delegates each to `RunParseCommandHandler.HandleAsync`. No parsing logic in the job itself. Comments in source confirm compliance gates (first-party allow-list / robots.txt / rate-limit — invariant #9) and admin-protected persist (`DoNotUpdate` / `DoNotParse` — invariant #3) are Step 9's concern.

**Known deferral documented:**
- `WorkerCompositionRoot.cs` lines 64–71: explicit `KNOWN DEFERRAL (Step 13)` comment naming `ICatalogMenuWriter`, `ICatalogDishResolver`, `IRestaurantGeocoder` as the unimplemented production adapters, explaining the Worker boots but `WeeklyParseJob` throws at first fire, and that Step 13 is the owner.
- `implementation.md` Step 12: "Known deferral" block names Step 13.

## Step 11 QA Findings (2026-06-17)

### Verification Summary
All Step 11 acceptance criteria verified against real behavior.

### Build
- `dotnet build src\WhereToEat.sln`: exit 0, 0 warnings, 0 errors.

### Test Counts (all green)
- Analytics.Domain.UnitTests: **11/11**
- Analytics.Application.UnitTests: **24/24** (includes AnonymizerTests, RotatingSaltProviderTests, AddAnalyticsModuleGuardTests, IngestEventCommandHandlerTests)
- Analytics.Integration (isolated): **9/9** — real Testcontainers SQL Server confirmed from log (Docker 28.4.0, SQL Server 2022 container)
- ArchitectureTests: **33/33** unchanged

### Acceptance Criteria Results

**Anonymization at ingest (invariant #11):**
- `Anonymizer.Anonymize` applies retain/hash/drop before any persistence: retained dimensions (restaurant/category/dish ids, sort, filters, position) pass verbatim; time is hour-truncated; lat/lng is coarsened to neighbourhood-grade geohash (≤5 chars at MaxPrecision); raw user/session id → HMAC-SHA256 hex digest (non-reversible, not brute-forceable without master secret).
- Integration test `Ingest_Persists_OnlyTheAnonymizedShape` confirmed: ActorHash = 64-char hex not containing raw id; CoarseGeohash ≤6 chars not containing raw coordinate strings.
- Negative test `StoredRow_NeverPopulates_APreciseCoordinateOrRawPiiColumn`: (a) schema check via `sys.columns` confirms no column named UserId/SessionId/Latitude/Longitude/Lat/Lng/Ip/IpAddress/Email exists; (b) LIKE scan across all textual columns confirms raw id and full precise coordinate strings absent from every stored row.

**Rotating salt:**
- `RotatingSaltProvider`: salt = `{MasterSecret}:{windowIndex}` where windowIndex = `ticks / windowTicks`. Same instant within a window → identical salt; next window → different salt. Tests `Salt_IsStable_WithinTheSameWindow` and `Salt_Diverges_AcrossWindows` green.
- `Anonymizer` tests `Anonymize_SameRawId_SameSalt_ProducesTheSameHash` and `Anonymize_SameRawId_DifferentSalt_ProducesADifferentHash` confirm funnel-stable / not-cross-window-linkable behavior.

**HMAC secret guard (security):**
- `AddAnalyticsModule` throws `InvalidOperationException` on blank `MasterSecret` in Production/Staging environments (guard test: `AddAnalyticsModule_RejectsABlankMasterSecret_OutsideDevOrTesting`).
- Blank secret tolerated in Development/Testing (guard test: `AddAnalyticsModule_Tolerates_ABlankMasterSecret_InDevOrTesting`).
- Non-blank secret in Production/Staging accepted without exception.
- Salt incorporates MasterSecret so HMAC key is not public-only.

**Aggregates only:**
- `IAnalyticsRollupReader.GetRestaurantHourlyRollupAsync` returns `RestaurantHourlyRollup` DTO containing only `(RestaurantId, HourUtc, Impressions, CardOpens, Ctr)` — no actor hash, no geohash, no per-event id in return type.
- Integration test `RollupReader_Returns_AggregatesOnly_ImpressionsAndCtr`: 4 impressions + 1 card-open for a restaurant → rollup returns Impressions=4, CardOpens=1, Ctr≈0.25 for the correct hour bucket.

**Append-optimized batch insert:**
- `AppendOnlyAnalyticsWriter.AppendBatchAsync` passes array of parameter objects to `Dapper.ExecuteAsync` (one round-trip for entire batch).
- Integration test `AppendBatch_InsertsEveryEvent_InOneRoundTrip` (5 events) green.
- `AppendBatch_WithEmptyCollection_IsANoOp` green (no exception).

**Dapper only (no EF):**
- `WhereToEat.Analytics.Infrastructure.csproj` references only `Dapper` and `Microsoft.Extensions.DependencyInjection.Abstractions` — no EF Core package reference.
- EF-containment architecture test still green.

**Migration 0012:**
- `0012_create_analytics_rollups.sql` creates `analytics.RestaurantHourlyRollup` with `(RestaurantId, HourUtc, Impressions, CardOpens)` and PK. No actor hash, no geohash column by schema.
- Integration test `Migrations_CreatedTheRollupTable` confirms script applied.

**Endpoint:**
- `POST /analytics/events` in `AnalyticsEndpoints.cs` wired in PublicApi; accepts anonymous requests; validates EventKind before building command; returns 202 on success, 400 on bad input.

## Step 10 QA Findings (2026-06-16)

### Verification Summary
All Step 10 acceptance criteria verified against real behavior.

### Build
- `dotnet build src\WhereToEat.sln`: exit 0, 0 warnings, 0 errors.

### Test Counts (all green)
- AdminApi.Integration: **15/15** (Testcontainers SQL container confirmed from log output)
- ArchitectureTests: **33/33** (including EF-containment rule)
- Admin.Application.UnitTests: **7/7**
- Admin.Domain.UnitTests: **4/4**
- All other existing tests: unchanged and passing.
- Analytics.Integration (5 tests): passes in isolation; transient Docker port collision when full suite runs in parallel — not a Step 10 defect.

### Acceptance Criteria Results

**Distinct admin host / admin-only auth:**
- `WhereToEat.AdminApi` is a separate composition root on port 5180 (own csproj/Program.cs/appsettings).
- `AdminAuthorization` policy uses `AdminRoleRequirement` + `AdminRoleHandler` resolving `IClaimsToRoleMapper` from DI (swappable).
- Integration tests: anonymous → 401, authenticated non-admin (`user` role) → 403, admin → 204. All three paths verified.

**EF database-first, migrations disabled:**
- No `Migrations` folder in `WhereToEat.Admin.Infrastructure`; `GetMigrations()` returns empty.
- `EfMigrationsDisabledStartupFilter` registered as `IStartupFilter`, runs eagerly at host build.
- Guard passes against the script-created schema; fails with `AdminDbContext.MissingTable` error code when pointed at `tempdb`.
- EF context configured with `MigrationsAssembly` pinned to Admin.Infrastructure (no stray migrations).

**EF containment:**
- `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.SqlServer` referenced ONLY in `WhereToEat.Admin.Infrastructure.csproj`.
- `EfCoreContainmentTests.EntityFrameworkCore_IsDependedOnOnlyBy_AdminInfrastructure` passes in 33/33 ArchitectureTests run.

**Admin > parser protection (invariant #3):**
- `DoNotUpdate_RestaurantIsSkippedEntirely_ByTheParse`: admin sets flag via real HTTP endpoint; parser's `RunParseCommandHandler` observes the flag read via Dapper from the same DB EF wrote — returns `SkippedDoNotUpdate`, no items added, curated price untouched.
- `DoNotParse_ItemSurvives_WhileUnprotectedItemIsOverwritten`: protected item keeps admin price (200 UAH); unprotected sibling updated to new parse price (85 UAH). Enforcement is in production `RunParseCommandHandler` (lines ~131–158), not a test stub.

**EditAddress → re-geocode:**
- `EditAddress_RaisesReGeocode_InvokingTheGeocoderWithTheNewAddress`: address persisted to DB AND `IGeocoder.GeocodeAsync` invoked with new address text (confirmed by NSubstitute Received(1) assertion). The flow: admin endpoint → `EditAddressCommandHandler` → `IAddressChangedDispatcher` → `InProcessAddressChangedDispatcher` → `GeocodeOnAddressChangedHandler`.

**No secrets in production appsettings:**
- `appsettings.json`: connection string = `""`, authority = `""`. Production values come from environment/config override.
- `appsettings.Development.json`: contains a local dev placeholder (`Your_strong_Pass123` for local Docker compose). Same pattern as Step 6 PublicApi — accepted dev convention, not a production secret.
