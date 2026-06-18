using FluentAssertions;
using Xunit;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Pins the module-grouping rule (implementation.md conventions + Step 2) so the Search-naming
/// decision is mechanically enforced: <c>WhereToEat.Catalog.Search.*</c> must resolve to the
/// <c>Catalog</c> module (search is a Catalog sub-module), while a normal module assembly
/// resolves to its own first segment. If anyone "promotes" Search into its own module by
/// changing the helper, this suite fails.
/// </summary>
public sealed class ModuleGroupingTests
{
    [Theory]
    // The decisive special case: every Catalog.Search.* layer belongs to the Catalog module.
    [InlineData("WhereToEat.Catalog.Search.Application", "Catalog")]
    [InlineData("WhereToEat.Catalog.Search.Infrastructure", "Catalog")]
    [InlineData("WhereToEat.Catalog.Search.Domain", "Catalog")]
    // Plain Catalog layers are obviously the Catalog module too.
    [InlineData("WhereToEat.Catalog.Domain", "Catalog")]
    [InlineData("WhereToEat.Catalog.Application", "Catalog")]
    [InlineData("WhereToEat.Catalog.Infrastructure", "Catalog")]
    // A normal, unrelated module resolves to its own first segment (NOT special-cased).
    [InlineData("WhereToEat.Recommendation.Domain", "Recommendation")]
    [InlineData("WhereToEat.Recommendation.Application", "Recommendation")]
    [InlineData("WhereToEat.Recommendation.Infrastructure", "Recommendation")]
    [InlineData("WhereToEat.Ratings.Domain", "Ratings")]
    [InlineData("WhereToEat.Admin.Infrastructure", "Admin")]
    [InlineData("WhereToEat.Geo.Application", "Geo")]
    public void ResolveModule_ReturnsExpectedModule(string assemblyName, string expectedModule)
    {
        ModuleGrouping.ResolveModule(assemblyName).Should().Be(expectedModule);
    }

    [Theory]
    // Shared families are deliberately NOT modules — they exist to be shared across module lines.
    [InlineData("WhereToEat.SharedKernel")]
    [InlineData("WhereToEat.Contracts")]
    [InlineData("WhereToEat.BuildingBlocks.Persistence")]
    [InlineData("WhereToEat.BuildingBlocks.Auth")]
    // Hosts are composition roots that legitimately reference many modules — also not modules.
    [InlineData("WhereToEat.PublicApi")]
    [InlineData("WhereToEat.AdminApi")]
    [InlineData("WhereToEat.Worker")]
    // Outside our root, or too short to carry a module segment.
    [InlineData("System.Text.Json")]
    [InlineData("WhereToEat")]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveModule_ForNonModules_ReturnsNull(string? assemblyName)
    {
        ModuleGrouping.ResolveModule(assemblyName).Should().BeNull();
    }

    [Fact]
    public void AreDifferentModules_ForCatalogAndItsSearchSubModule_IsFalse()
    {
        // The crux: a reference between Catalog.* and Catalog.Search.* is same-module, so the
        // cross-module boundary rules must NOT flag it.
        ModuleGrouping
            .AreDifferentModules("WhereToEat.Catalog.Search.Application", "WhereToEat.Catalog.Domain")
            .Should().BeFalse();
    }

    [Fact]
    public void AreDifferentModules_ForTwoLayersOfTheSameModule_IsFalse()
    {
        ModuleGrouping
            .AreDifferentModules("WhereToEat.Recommendation.Application", "WhereToEat.Recommendation.Domain")
            .Should().BeFalse();
    }

    [Fact]
    public void AreDifferentModules_ForGenuineCrossModuleReference_IsTrue()
    {
        // A real leak: Catalog reaching into Recommendation's internals.
        ModuleGrouping
            .AreDifferentModules("WhereToEat.Catalog.Application", "WhereToEat.Recommendation.Domain")
            .Should().BeTrue();

        // And the Search sub-module reaching into a different module is still a true leak.
        ModuleGrouping
            .AreDifferentModules("WhereToEat.Catalog.Search.Application", "WhereToEat.Ratings.Domain")
            .Should().BeTrue();
    }

    [Fact]
    public void AreDifferentModules_WhenEitherSideIsShared_IsFalse()
    {
        // Referencing a shared family (Contracts/SharedKernel) is always allowed, never a
        // cross-module leak — that is the whole point of the shared families.
        ModuleGrouping
            .AreDifferentModules("WhereToEat.Recommendation.Application", "WhereToEat.Contracts")
            .Should().BeFalse();

        ModuleGrouping
            .AreDifferentModules("WhereToEat.Contracts", "WhereToEat.Recommendation.Application")
            .Should().BeFalse();
    }
}
