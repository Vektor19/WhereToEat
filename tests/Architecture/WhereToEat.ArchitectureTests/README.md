# WhereToEat.ArchitectureTests

Build-failing **architecture-fitness tests** (Step 2). They mechanically enforce the
modular-monolith boundaries the design commits to, so a boundary violation breaks the build
instead of relying on code review.

## CI: these tests are NON-SKIPPABLE

`dotnet test` over the solution (or `dotnet test tests/Architecture/WhereToEat.ArchitectureTests`)
**must** run on every commit and every later step, and must be **green** before the next step
starts. Do **not** add `[Fact(Skip = ...)]`, `[Trait("Category","Skip")]`, a CI filter that
excludes this project, or `--filter` expressions that drop these tests. They are the guardrail
for the whole solution; skipping them silently re-opens the boundary erosion they prevent.

## How the rules grow automatically

The rules **discover assemblies by naming convention at runtime** — every `WhereToEat.*`
assembly present next to the test assembly is loaded and asserted against (see `AssemblyLoader`).
Most module assemblies do not exist yet, so several rules are **vacuously true today** and start
biting as modules are added. To bring a new module under the guards, add a `ProjectReference` to
it in `WhereToEat.ArchitectureTests.csproj` so its assembly lands in this project's output.

## Rules

Rules (a)–(d) are driven by **NetArchTest's fluent API** (`Types.InAssemblies(...).ShouldNot().HaveDependencyOn...`)
over the convention-discovered assemblies. Rule (e) and the `ModuleGrouping` helper stay on plain
`System.Reflection` — a member-signature scan / pure string logic NetArchTest's predicate model
cannot express.

| File | Engine | Enforces |
| --- | --- | --- |
| `ModuleGrouping` / `ModuleGroupingTests` | Reflection (string logic) | The module-grouping rule: a module is the first namespace segment after `WhereToEat`, with `WhereToEat.Catalog.Search.*` resolving to the `Catalog` module. Feeds the namespace sets the NetArchTest module rule asserts on. |
| `LayerDependencyTests` | NetArchTest | (a) `*.Domain` depends on nothing in `WhereToEat.*` except `WhereToEat.SharedKernel`; (b) `*.Application` depends on no `*.Infrastructure` and no `Host`. |
| `EfCoreContainmentTests` | NetArchTest | (c) EF Core (`Microsoft.EntityFrameworkCore*`) is depended on **only** by `WhereToEat.Admin.Infrastructure`. |
| `ModuleBoundaryTests` | NetArchTest | (d) no module depends on a **different** module's (non-`Contracts`) namespaces. |
| `MessageConsumerBoundaryTests` | Reflection | (e) MassTransit consumers reference only `WhereToEat.Contracts` types across module lines, never another module's Domain/Application internals (instance **and** static members scanned). |

## Proving the guards bite (introduce-then-revert)

NetArchTest analyses **IL-level type usage** (it reads each type's referenced types via Mono.Cecil),
which is what makes rules (a)–(d) robust. A consequence worth knowing: a *bare, unused*
`PackageReference` — or use that the compiler folds to a constant (e.g. `nameof(DbContext)`) —
leaves **no** type reference in IL and is invisible to the rules. That is acceptable: the rules
fire the moment a forbidden dependency is *actually used* (the only case that matters), but it
means a negative check must use the offending type in a **signature or body**.

Verified negative check for the EF-containment guard (introduce → observe failure → revert):

1. Temporarily pin `Microsoft.EntityFrameworkCore` in `Directory.Packages.props`, add a
   `PackageReference` to it in `WhereToEat.SharedKernel.csproj`, and add a throwaway member whose
   **signature** uses an EF type, e.g.:
   ```csharp
   internal static class TempEfViolation { public static DbContextId? Marker { get; set; } }
   ```
2. `dotnet test` → `EfCoreContainmentTests.EntityFrameworkCore_IsDependedOnOnlyBy_AdminInfrastructure`
   **fails**, naming `WhereToEat.SharedKernel.TempEfViolation` as the offending type.
3. Revert all three edits; `dotnet test` is green again.

This negative check was run during Step 2 to confirm the NetArchTest-expressed EF-containment rule
actually fires on a violation. The same shape proves the other NetArchTest rules (point a Domain
type at any non-SharedKernel `WhereToEat.*` namespace, or have a module's type depend on another
module's namespace).
