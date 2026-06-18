using FluentAssertions;
using NetArchTest.Rules;

namespace WhereToEat.ArchitectureTests;

/// <summary>
/// Bridges a NetArchTest <see cref="TestResult"/> into an xUnit/FluentAssertions assertion with a
/// descriptive failure message that names the offending types (Step 2). NetArchTest evaluates the
/// rule (which types depend on what, at the IL level); this helper just turns its boolean outcome
/// into a readable test failure, so the dependency-direction, EF-containment, and module-isolation
/// rules read uniformly.
/// </summary>
internal static class NetArchTestExtensions
{
    /// <summary>
    /// Asserts the NetArchTest rule held, failing with <paramref name="because"/> plus the
    /// NetArchTest-reported failing type names when it did not.
    /// </summary>
    public static void ShouldHold(this TestResult result, string because)
    {
        var failingTypes = result.FailingTypeNames ?? Array.Empty<string>();

        result.IsSuccessful.Should().BeTrue(
            "{0} Offending types: {1}",
            because,
            failingTypes.Count == 0 ? "(none reported)" : string.Join(", ", failingTypes));
    }
}
