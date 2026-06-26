# AGENTS.md

Institutional knowledge for the agent orchestration system. The `reflect` agent appends durable, generalizable learnings here at the end of each session. Keep entries short and actionable.

## Learnings

<!-- reflect appends concise, reusable learnings below -->

### Crash/session-limit recovery in long multi-step orchestrations
- When a subagent session is interrupted mid-step, re-entry should begin by reading `implementation.md` for the step's status and then checking `git status --short` (or the filesystem directly) to determine what is already on disk. Newly created files are staged by default in this repo, so `git ls-files --others` misses them; `git status --short` or direct filesystem inspection is authoritative.
- Delegate re-entry as "complete and finalize step N" rather than "start step N from scratch" — avoids re-doing work that was already partially applied.

### Docker daemon must be running before any Testcontainers step
- Docker Desktop stops between long sessions. Before delegating any step that contains integration tests (Testcontainers), check that the Docker daemon is reachable (`docker info` exit 0) and start Docker Desktop if needed. Do not hand off to the developer until Docker is confirmed running.

### Testcontainers parallel-run flake: serialize integration assemblies
- Running `dotnet test` on the full solution in parallel causes Testcontainers containers to race for Docker resources, producing intermittent startup failures. Mitigate by adding `tests/WhereToEat.runsettings` (MaxCpuCount=1) and `tests/xunit.runner.json` (parallelizeAssembly/Collections false) copied to every test output via `tests/Directory.Build.targets`. Always run the full suite with `--settings tests/WhereToEat.runsettings`. Verify a flaky project in isolation before escalating as a real failure.

### Architecture-fitness rules must be verified as non-vacuous
- A NetArchTest rule passes silently (vacuously) when the target assembly is not in the scanned set. Every time a new module is added or a new fitness rule is written, (a) add a `ProjectReference` for every new module assembly to the architecture-test project, and (b) perform a "negative-check / introduce-then-revert" probe to confirm the rule fires on a deliberate violation. If the probe does NOT fail, the rule is vacuous and must be fixed before treating it as a real guard.

### Wire production adapters behind cross-cutting seams before the first end-to-end job runs
- Seams backed by test doubles (e.g., `ICatalogMenuWriter`, `ICatalogDishResolver`, `IRestaurantGeocoder`) hide latent runtime bugs — the deferred production implementation may have type-mapping issues (e.g., Dapper cannot map a multi-column result set into a `record struct`) that only appear when the real adapter is substituted. Use `BuildServiceProvider(validateOnBuild: true)` + `GetRequiredService<T>()` in a no-Docker unit test to catch unregistered seams early, and prefer switching to production adapters as soon as they exist rather than leaving doubles in place across multiple steps.

### Security seams need environment-gating and fail-fast guards in production
- Test-only JWT signing keys (OIDC authority blank, HS256 test key) and secrets such as the analytics HMAC master secret must be tolerated only in `Development`/`Testing` environments and must throw `InvalidOperationException` at startup in `Production`/`Staging`. Add the guard inside the module's `AddXxxModule` registration extension (not as a runtime check), so it fires at host build time and is covered by a unit test. Mirror this pattern for every new security seam.

### Logging in .NET 8 with TreatWarningsAsErrors: use [LoggerMessage] source generation
- CA1848 ("use LoggerMessage delegates for performance") is a warning that becomes an error under `TreatWarningsAsErrors`. Use `[LoggerMessage]` partial methods on a `partial` class instead of `ILogger.Log(...)` calls. Establish this convention on the first module that introduces logging so all subsequent modules follow the same pattern.

### FakeTimeProvider for rate-limit/timing tests: advance the clock on the test thread, never via a background pump
- Driving `FakeTimeProvider` from a background busy-wait loop causes `Task.Delay` (which awaits the fake clock) to hang indefinitely and freeze `dotnet test`. Always advance the fake clock synchronously on the test thread after the awaitable is set up (typically: start the awaiting task, advance the clock, then await the task).

### dotnet sln add creates duplicate virtual folder nodes; hand-edit the .sln instead
- `dotnet sln add` injects a duplicate physical folder path into the `.sln` nested-projects section when the target solution folder already exists under a different GUID. For a solution with established virtual-folder GUIDs (00-Shared, 01-Modules, 02-Hosts, 03-Tests), hand-edit the `.sln` to add the correct `Project(...)` entry and `NestedProjects` mapping rather than using `dotnet sln add`.

### XmlComment `--` in .runsettings causes a parse error
- `.runsettings` is an XML file; the sequence `--` inside an XML comment is invalid and causes the test runner to fail with a parse error. Use single-dash comments or rephrase to avoid double-dash sequences inside `<!-- -->` blocks.

### Angular 21 + Vitest unit testing: no zone.js, no fakeAsync
- Angular 21 drops zone.js from the unit-test runner; `fakeAsync`/`tick` do not work. Use `vi.useFakeTimers()` + `vi.advanceTimersByTimeAsync()` for time-sensitive tests (debounce, flush-on-timeout). Advance the clock synchronously on the test thread after starting the awaitable — never from a background pump (see the existing FakeTimeProvider learning for the same principle applied to .NET).

### Angular 21 + ngx-translate under Vitest: JIT compiler polyfill is mandatory
- `@ngx-translate/core` v18 requires the Angular JIT compiler at test time but the Angular 21 unit-test builder omits it by default. Add a separate `build:test` configuration in `angular.json` with `"polyfills": ["@angular/compiler"]`, point the test target's `buildTarget` at it, and wire a `providersFile` (e.g. `src/test-providers.ts`) that provides the translate service with an in-memory loader so all component specs get translated text rather than raw keys. Without the polyfill the translate module fails to initialise and every spec using translated copy breaks.

### Angular signals — NG0600 in lazy-creating store accessors and computed-inside-computed
- Reading a store accessor that lazily creates a signal (e.g. `dishesByCategory.get(key) ?? createSignal()`) inside a `computed()` triggers a signal write during a read, causing NG0600 at runtime. Fix by returning a stable idle-sentinel signal created once at store construction time instead of creating a new signal on each read. Similarly avoid allocating new `computed()` calls inside an outer `computed()` — compute the value directly instead of nesting computed factories.

### Angular component style budgets with single-file standalone components
- Co-locating template + styles in one standalone component file causes the `anyComponentStyle` budget to count every style token, responsive-grid rule, and multi-state selector against a single component. The Angular CLI default warn ceiling of 4 kB is too tight for shells and rich cards in this pattern. Raise the warn budget to 6–8 kB (keep the error ceiling at 8–10 kB) and document the justification; this avoids noisy warnings masking real bloat. A budget-free build should be a hard gate in CI — warnings that are always present are ignored.

### Backend wire-contract details the frontend must encode, not discover at integration time
- Several .NET wire-contract conventions are non-obvious and caused frontend rework when discovered late:
  1. **Enum binding by integer ordinal**: if a .NET enum lacks `[JsonStringEnumConverter]`, the client must send the integer value (`1`/`2`), not the string name (`"Basic"`/`"Pro"`). Encode ordinal maps on the frontend side explicitly (e.g. `VERIFIED_TIER_ORDINALS`).
  2. **EventKind enum members vs field names**: the analytics ingest parser is case-insensitive on member names but does NOT strip underscores, so send `CardOpen` not `card_open`. Equally, the `EventKind` enum has no `Geo` member — geo data rides as explicit `latitude`/`longitude` fields, never a kind; sending an unknown kind returns 400.
  3. **Nested admin path doubling**: a single-origin API gateway that rewrites `/api/admin` → `""` results in URLs like `/api/admin/admin/analytics/...` — the `/admin` prefix appears twice. Verify the composed URL in a unit test with `HttpTestingController` on day one.
  4. **Default camelCase JSON**: ASP.NET Core minimal APIs serialize property names camelCase by default; ensure DTO field names in TypeScript match exactly.
  Document these conventions per backend module as soon as the module's endpoints are designed, so the frontend data-access layer can be written once correctly.

### Playwright route-mock registration order matters
- Playwright matches `page.route()` mocks in reverse registration order (last registered wins). Register broad catch-all mocks first so specific endpoint mocks added afterward take precedence. Registering a `**/api/**` wildcard last will shadow all specific mocks below it.

### Angular HttpParams does not percent-encode colons in query values
- `new HttpParams().set('from', isoString)` leaves the `:` characters in the ISO-8601 value unencoded in the final URL. `HttpTestingController.expectOne(url)` matches against the raw (unencoded) URL string, so assertions must use the raw ISO string — not `encodeURIComponent(value)`. Encoding the expectation causes `expectOne` to find nothing, leaving the request open and producing cascading verification failures in subsequent tests.

### Portability via clean architecture, not literal code sharing
- An early plan assumed "portable core = zero Angular imports so modules can be copied to React Native verbatim." The correct approach for a framework-aware stack is: put pure business logic (formatters, builders, validators, Haversine, event builders, stores backed by signals) in a `core` library with a clean public API and no UI-framework coupling; achieve cross-platform portability by rewriting the thin UI/adapter layer for each platform, not by dragging framework-free code into a framework project. Clean module boundaries + a well-typed port interface make rewriting cheap; literal code copying does not scale.
