namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Resolves which **module** an assembly belongs to, per this plan's module-grouping rule
/// (implementation.md, "Conventions for this plan" + Step 2). A module is the first namespace
/// segment after <c>WhereToEat</c> (e.g. <c>Catalog</c>, <c>Recommendation</c>, <c>Ratings</c>),
/// with the single special case that <c>WhereToEat.Catalog.Search.*</c> resolves to the
/// <c>Catalog</c> module (deterministic search is a Catalog sub-module, not a module of its own).
/// Shared families (<c>SharedKernel</c>, <c>Contracts</c>, <c>BuildingBlocks.*</c>) and
/// <c>Host</c> assemblies are NOT modules and resolve to <c>null</c>.
///
/// The cross-module fitness rules use this helper, so an intra-<c>Catalog</c> reference between
/// <c>Catalog.*</c> and <c>Catalog.Search.*</c> is treated as same-module and is never flagged,
/// while a genuine cross-module leak still is. Pure string logic so it is unit-testable
/// (see <see cref="ModuleGroupingTests"/>) without loading any assembly.
/// </summary>
internal static class ModuleGrouping
{
    private const string RootPrefix = "WhereToEat";

    /// <summary>
    /// Shared families that are deliberately not modules — they exist precisely to be shared
    /// across module lines, so cross-"module" rules must skip them. Matched as the first
    /// segment after <c>WhereToEat</c> (e.g. <c>WhereToEat.BuildingBlocks.Persistence</c>).
    /// </summary>
    private static readonly HashSet<string> SharedFamilies = new(StringComparer.Ordinal)
    {
        "SharedKernel",
        "Contracts",
        "BuildingBlocks",
    };

    /// <summary>
    /// Returns the module name for an assembly name like <c>WhereToEat.Catalog.Domain</c>
    /// (→ <c>Catalog</c>) or <c>WhereToEat.Catalog.Search.Application</c> (→ <c>Catalog</c>),
    /// or <c>null</c> for shared families, hosts, and anything outside the <c>WhereToEat</c>
    /// root. The assembly's name and its root namespace share the same convention, so the same
    /// resolver serves both NetArchTest dependency strings and assembly identities.
    /// </summary>
    public static string? ResolveModule(string? assemblyOrNamespace)
    {
        if (string.IsNullOrWhiteSpace(assemblyOrNamespace))
        {
            return null;
        }

        var segments = assemblyOrNamespace.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Must be rooted at WhereToEat.<segment> to be one of our modules at all.
        if (segments.Length < 2 || !string.Equals(segments[0], RootPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var first = segments[1];

        // Shared families and Host assemblies are not modules.
        if (SharedFamilies.Contains(first) || IsHostSegment(first))
        {
            return null;
        }

        // Special case: WhereToEat.Catalog.Search.* belongs to the Catalog module, not a
        // "Search" module — search is a Catalog sub-module (implementation.md conventions).
        // (Already covered by returning `first` == "Catalog", but kept explicit so the decision
        // is self-documenting and the intent survives future refactors.)
        if (string.Equals(first, "Catalog", StringComparison.Ordinal))
        {
            return "Catalog";
        }

        return first;
    }

    /// <summary>
    /// True when the assembly/namespace belongs to a real module (not a shared family or host).
    /// </summary>
    public static bool IsModuleAssembly(string? assemblyOrNamespace)
        => ResolveModule(assemblyOrNamespace) is not null;

    /// <summary>
    /// True when both names resolve to modules and those modules differ — i.e. a reference
    /// between them is a genuine cross-module reference the boundary rules care about.
    /// Same-module references (incl. <c>Catalog.*</c> ↔ <c>Catalog.Search.*</c>) return false.
    /// </summary>
    public static bool AreDifferentModules(string? a, string? b)
    {
        var moduleA = ResolveModule(a);
        var moduleB = ResolveModule(b);

        return moduleA is not null
            && moduleB is not null
            && !string.Equals(moduleA, moduleB, StringComparison.Ordinal);
    }

    // A host assembly is named WhereToEat.<Something>(Api|Host|Worker). Hosts are composition
    // roots that legitimately reference many modules, so they are excluded from module rules.
    private static bool IsHostSegment(string segment)
        => segment.EndsWith("Api", StringComparison.Ordinal)
            || segment.EndsWith("Host", StringComparison.Ordinal)
            || string.Equals(segment, "Worker", StringComparison.Ordinal);
}
