using NetArchTest.Rules;
using Xunit;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// The Step 14 organic-ranking-is-not-for-sale guard (invariant #10): the recommendation pipeline
/// MUST NOT depend on the Monetization module in any way — no Verified status, no ad placement, and no
/// payment type may ever reach the Step 7 organic score. This is a stricter, explicitly-named subset of
/// the general module-isolation rule (<see cref="ModuleBoundaryTests"/>): it singles out the
/// Recommendation → Monetization edge so a violation fails with a message that points straight at the
/// invariant it breaks. It would bite the instant any <c>WhereToEat.Recommendation.*</c> assembly took
/// a reference on any <c>WhereToEat.Monetization.*</c> assembly (a project reference, a using, or a type
/// use — NetArchTest inspects the compiled IL dependencies).
/// </summary>
public sealed class RecommendationDoesNotDependOnMonetizationTests
{
    [Fact]
    public void Recommendation_HasNoReferenceToMonetization()
    {
        var recommendationAssemblies = AssemblyLoader.ModuleAssemblies
            .Where(a => string.Equals(
                ModuleGrouping.ResolveModule(a.GetName().Name), "Recommendation", StringComparison.Ordinal))
            .ToArray();

        var monetizationNamespaces = AssemblyLoader.ModuleAssemblies
            .Where(a => string.Equals(
                ModuleGrouping.ResolveModule(a.GetName().Name), "Monetization", StringComparison.Ordinal))
            .Select(a => a.GetName().Name!)
            .ToArray();

        // Both modules must actually be loaded for this guard to be meaningful — if either is missing
        // the wiring is wrong (the architecture-test csproj references both as of Step 14).
        Assert.NotEmpty(recommendationAssemblies);
        Assert.NotEmpty(monetizationNamespaces);

        Types.InAssemblies(recommendationAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(monetizationNamespaces)
            .GetResult()
            .ShouldHold(
                "WhereToEat.Recommendation.* must have NO reference to WhereToEat.Monetization.* — " +
                "organic ranking is not for sale (invariant #10). An ad placement / Verified status / " +
                "payment type must never reach the Step 7 organic score.");
    }
}
