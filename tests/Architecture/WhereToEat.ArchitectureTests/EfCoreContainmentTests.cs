using NetArchTest.Rules;
using Xunit;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// EF-containment guard (Step 2, rule (c) — enforces the design's "EF Core scoped strictly to
/// admin CRUD" and the residual-risk mitigation "two data-access stacks must not bleed into
/// each other"). Entity Framework Core may be referenced **only** by
/// <c>WhereToEat.Admin.Infrastructure</c>; every other assembly's hot/read path is Dapper.
///
/// Expressed with **NetArchTest's fluent API**: every loaded <c>WhereToEat.*</c> type that does
/// NOT reside in <c>WhereToEat.Admin.Infrastructure</c> <c>ShouldNot().HaveDependencyOn</c> the
/// <c>Microsoft.EntityFrameworkCore</c> namespace (prefix-matched, so <c>.SqlServer</c>,
/// <c>.Relational</c>, etc. are all covered). Passes **vacuously** today (no assembly uses EF yet,
/// and Admin.Infrastructure does not exist until Step 10) and bites the moment EF is used anywhere
/// outside Admin.Infrastructure.
/// </summary>
public sealed class EfCoreContainmentTests
{
    // The single namespace allowed to depend on EF Core (assembly created in Step 10).
    private const string AllowedEfNamespace = "WhereToEat.Admin.Infrastructure";

    // EF Core namespaces all start with this prefix (Microsoft.EntityFrameworkCore,
    // Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Relational, …);
    // NetArchTest matches dependencies by namespace prefix, so this one string covers them all.
    private const string EfNamespacePrefix = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void EntityFrameworkCore_IsDependedOnOnlyBy_AdminInfrastructure()
    {
        Types.InAssemblies(AssemblyLoader.SolutionAssemblies)
            .That()
            .DoNotResideInNamespace(AllowedEfNamespace)
            .ShouldNot()
            .HaveDependencyOn(EfNamespacePrefix)
            .GetResult()
            .ShouldHold(
                $"EF Core may be referenced only by '{AllowedEfNamespace}' (the schema-of-record " +
                "is the SQL migration scripts; EF is confined to the low-traffic admin CRUD " +
                "surface).");
    }
}
