using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Module-isolation guard (Step 2, rule (d) — enforces the design's "modules talk to each other
/// only through published contracts"). No module's types may depend on a **different** module's
/// types: modules reach each other only through <c>WhereToEat.Contracts</c>, never into another
/// module's Domain/Application/Infrastructure internals. Cross-module coupling here would block the
/// later service-extraction the modular-monolith design depends on.
///
/// The rule is expressed with **NetArchTest's fluent API** (types in module M
/// <c>ShouldNot().HaveDependencyOnAny</c> the other modules' namespaces), while the pure,
/// unit-tested <see cref="ModuleGrouping"/> helper decides *which* assemblies are same-module vs.
/// cross-module — so a <c>Catalog.Search.*</c> ↔ <c>Catalog.*</c> reference is same-module and
/// allowed, and shared families (<c>Contracts</c>/<c>SharedKernel</c>/<c>BuildingBlocks.*</c>) are
/// never in the forbidden set. Vacuously true today (only the foundation assemblies exist, neither
/// is a module); guards every module pair as they are added.
/// </summary>
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void NoModuleDependsOnAnotherModulesInternals()
    {
        // Group the discovered module assemblies by their resolved module (Catalog, Ratings, …).
        var assembliesByModule = AssemblyLoader.ModuleAssemblies
            .GroupBy(a => ModuleGrouping.ResolveModule(a.GetName().Name)!, StringComparer.Ordinal)
            .ToList();

        foreach (var module in assembliesByModule)
        {
            // Forbidden = the root namespaces of every OTHER module's assemblies. Shared families
            // are already excluded (ModuleAssemblies only contains real modules), so the only thing
            // a module may still legally cross on (Contracts) is never in this list. Same-module
            // assemblies (incl. Catalog.* and Catalog.Search.*, which both resolve to "Catalog")
            // are excluded by the module-name comparison, so an intra-Catalog reference is allowed.
            var forbidden = AssemblyLoader.ModuleAssemblies
                .Where(a => !string.Equals(module.Key, ResolvedModuleOf(a), StringComparison.Ordinal))
                .Select(a => a.GetName().Name!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (forbidden.Length == 0)
            {
                // Only this one module is present — nothing cross-module to violate.
                continue;
            }

            Types.InAssemblies(module)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult()
                .ShouldHold(
                    $"module '{module.Key}' must reference other modules only through " +
                    "WhereToEat.Contracts, never another module's internal (non-Contracts) " +
                    "assemblies.");
        }
    }

    private static string ResolvedModuleOf(Assembly assembly)
        => ModuleGrouping.ResolveModule(assembly.GetName().Name)!;
}
