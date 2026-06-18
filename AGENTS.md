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
