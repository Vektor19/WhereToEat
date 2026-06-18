using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Dependency-direction guards (Step 2, rules (a) and (b)). Enforces the Onion/Clean rule from
/// the design (dependencies point inward: Infrastructure → Application → Domain) and the plan's
/// layer dependency rule, so a boundary violation breaks the build instead of relying on review.
///
/// These rules are expressed with **NetArchTest's fluent API** — types in a layer's namespaces
/// <c>ShouldNot().HaveDependencyOnAny(...)</c> the forbidden namespaces — so NetArchTest evaluates
/// the actual IL-level dependencies. The forbidden namespace sets are computed from the
/// convention-discovered <c>WhereToEat.*</c> assemblies (see <see cref="AssemblyLoader"/>), so the
/// rules grow automatically as later steps add module/layer assemblies. Both are vacuously true
/// today (no <c>*.Domain</c>/<c>*.Application</c> assembly exists until Step 3) and bite the moment
/// such an assembly takes a forbidden dependency.
/// </summary>
public sealed class LayerDependencyTests
{
    private const string SharedKernelAssembly = "WhereToEat.SharedKernel";
    private const string ContractsAssembly = "WhereToEat.Contracts";
    private const string DomainSuffix = ".Domain";
    private const string ApplicationSuffix = ".Application";
    private const string InfrastructureSuffix = ".Infrastructure";

    /// <summary>
    /// Rule (a): a <c>*.Domain</c> assembly depends on nothing in <c>WhereToEat.*</c> except
    /// <c>WhereToEat.SharedKernel</c>. Encodes the design's "Domain core has no framework or
    /// persistence dependencies" and keeps the domain testable in isolation. Vacuously true
    /// until Step 3 adds the first Domain assembly; it then guards every later Domain project.
    /// </summary>
    [Fact]
    public void Domain_DependsOnNothingInSolutionExcept_SharedKernel()
    {
        var domainAssemblies = AssemblyLoader.AssembliesWithLayerSuffix(DomainSuffix);

        // Every WhereToEat.* root namespace present in the build output is a candidate dependency.
        var ourNamespaces = AssemblyLoader.SolutionAssemblies
            .Select(a => a.GetName().Name!)
            .ToList();

        foreach (var domain in domainAssemblies)
        {
            var domainName = domain.GetName().Name!;

            // Forbidden = every WhereToEat.* namespace except SharedKernel and the Domain itself.
            var forbidden = ourNamespaces
                .Where(ns => !string.Equals(ns, SharedKernelAssembly, StringComparison.Ordinal))
                .Where(ns => !string.Equals(ns, domainName, StringComparison.Ordinal))
                .ToArray();

            if (forbidden.Length == 0)
            {
                // Only SharedKernel (and self) present — nothing this Domain could illegally reach.
                continue;
            }

            Types.InAssembly(domain)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult()
                .ShouldHold(
                    $"Domain assembly '{domainName}' may depend only on {SharedKernelAssembly} " +
                    "within our solution (Onion/Clean: dependencies point inward; Domain stays " +
                    "framework- and persistence-free).");
        }
    }

    /// <summary>
    /// Rule (b): an <c>*.Application</c> assembly does not depend on any <c>*.Infrastructure</c>
    /// or any <c>Host</c> assembly. Application orchestrates use-cases over ports; concrete
    /// adapters (Infrastructure) and entry points (Hosts) sit *outside* it — the inward arrow.
    /// </summary>
    [Fact]
    public void Application_DependsOnNoInfrastructureAndNoHost()
    {
        var applicationAssemblies = AssemblyLoader.AssembliesWithLayerSuffix(ApplicationSuffix);

        // The things Application must never reach: any module's Infrastructure or any Host.
        var forbidden = AssemblyLoader.NamespacesWithLayerSuffix(InfrastructureSuffix)
            .Concat(AssemblyLoader.HostNamespaces())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (forbidden.Length == 0)
        {
            // No Infrastructure/Host assemblies exist yet — the rule is vacuously satisfied, but
            // assert the discovery floor so a future empty set is a deliberate state, not a bug.
            applicationAssemblies.Should().NotBeNull();
            return;
        }

        foreach (var application in applicationAssemblies)
        {
            Types.InAssembly(application)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult()
                .ShouldHold(
                    $"Application assembly '{application.GetName().Name}' must not depend on any " +
                    "Infrastructure or Host assembly (dependencies point inward; Application " +
                    "depends only on Domain + ports).");
        }
    }

    /// <summary>
    /// Sanity guard on the loader itself: the two foundation assemblies that exist today must be
    /// discovered, so the suite is never silently empty (a green run with zero assemblies loaded
    /// would be a false pass). As modules arrive, this still holds — it only asserts a floor.
    /// </summary>
    [Fact]
    public void Loader_DiscoversTheFoundationAssemblies()
    {
        var names = AssemblyLoader.SolutionAssemblies
            .Select(a => a.GetName().Name)
            .ToList();

        names.Should().Contain(SharedKernelAssembly);
        names.Should().Contain(ContractsAssembly);
    }
}
