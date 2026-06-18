using System.Reflection;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Convention-based discovery of the solution's assemblies for the fitness rules (Step 2).
/// Rather than naming every module assembly (most do not exist yet), the rules load **all
/// <c>WhereToEat.*</c> assemblies present in the build output** and assert against whatever is
/// there — so the same tests grow automatically as later steps add modules (each new module
/// is referenced by the test project's csproj, lands in the output folder, and is then guarded).
/// </summary>
internal static class AssemblyLoader
{
    // Our own assemblies all share this prefix; test assemblies are excluded so the fitness
    // rules never police the test projects themselves.
    private const string OurPrefix = "WhereToEat.";
    private const string TestSuffix = "Tests";

    /// <summary>
    /// All loaded <c>WhereToEat.*</c> production assemblies discovered next to the running test
    /// assembly, excluding test assemblies. Loaded once and reused across all rule classes.
    /// </summary>
    public static IReadOnlyList<Assembly> SolutionAssemblies { get; } = LoadSolutionAssemblies();

    /// <summary>
    /// Production assemblies whose name (and root namespace) resolve to a real module via
    /// <see cref="ModuleGrouping"/> — i.e. excluding SharedKernel/Contracts/BuildingBlocks/Hosts.
    /// </summary>
    public static IReadOnlyList<Assembly> ModuleAssemblies { get; } =
        SolutionAssemblies.Where(a => ModuleGrouping.IsModuleAssembly(a.GetName().Name)).ToList();

    /// <summary>
    /// Returns the loaded assemblies whose simple name ends with the given layer suffix
    /// (e.g. <c>".Domain"</c>, <c>".Application"</c>, <c>".Infrastructure"</c>). Suffix match on
    /// the full simple name so <c>WhereToEat.Catalog.Search.Application</c> counts as Application.
    /// </summary>
    public static IReadOnlyList<Assembly> AssembliesWithLayerSuffix(string layerSuffix)
        => SolutionAssemblies
            .Where(a => (a.GetName().Name ?? string.Empty)
                .EndsWith(layerSuffix, StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// Root namespaces (== assembly simple names, by our convention) of every loaded
    /// <c>WhereToEat.*</c> assembly whose simple name ends with the given layer suffix. These
    /// feed NetArchTest's prefix-based <c>HaveDependencyOnAny(...)</c> dependency predicate.
    /// </summary>
    public static IReadOnlyList<string> NamespacesWithLayerSuffix(string layerSuffix)
        => AssembliesWithLayerSuffix(layerSuffix)
            .Select(a => a.GetName().Name!)
            .ToList();

    /// <summary>
    /// Root namespaces of the loaded <c>Host</c> assemblies (named <c>*Api</c>/<c>*Host</c>/
    /// <c>*.Worker</c>) — composition roots that the Application layer must not depend on.
    /// </summary>
    public static IReadOnlyList<string> HostNamespaces()
        => SolutionAssemblies
            .Select(a => a.GetName().Name!)
            .Where(IsHostAssemblyName)
            .ToList();

    private static bool IsHostAssemblyName(string assemblyName)
        => assemblyName.EndsWith("Api", StringComparison.Ordinal)
            || assemblyName.EndsWith("Host", StringComparison.Ordinal)
            || assemblyName.EndsWith(".Worker", StringComparison.Ordinal);

    private static List<Assembly> LoadSolutionAssemblies()
    {
        var directory = Path.GetDirectoryName(typeof(AssemblyLoader).Assembly.Location)!;

        var assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);

        foreach (var dllPath in Directory.EnumerateFiles(directory, $"{OurPrefix}*.dll"))
        {
            var simpleName = Path.GetFileNameWithoutExtension(dllPath);

            // Skip the architecture-test assembly and any other *Tests assembly — the rules
            // guard production code, never the tests that assert them.
            if (simpleName.EndsWith(TestSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            if (assemblies.ContainsKey(simpleName))
            {
                continue;
            }

            try
            {
                // Prefer an already-loaded instance (so identity matches NetArchTest's view),
                // otherwise load from the path on disk.
                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal))
                    ?? Assembly.LoadFrom(dllPath);

                assemblies[simpleName] = loaded;
            }
            catch (BadImageFormatException)
            {
                // Native/mixed image that is not a managed assembly — not one of ours to guard.
            }
        }

        return assemblies.Values.ToList();
    }
}
