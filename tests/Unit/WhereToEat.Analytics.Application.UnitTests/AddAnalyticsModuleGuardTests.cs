using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.Analytics.Infrastructure.Anonymization;
using WhereToEat.Analytics.Infrastructure.DependencyInjection;
using Xunit;

namespace WhereToEat.Analytics.Application.UnitTests;

/// <summary>
/// Proves the privacy fail-fast in <see cref="AnalyticsInfrastructureServiceCollectionExtensions.AddAnalyticsModule"/>:
/// outside Development/Testing a blank salt master secret is rejected at composition time (invariant
/// #11) — otherwise the actor-id HMAC would key on the public salt-window index alone and become
/// brute-forceable. In Development/Testing a blank secret stays tolerated, matching how the OIDC
/// authority guard relaxes only in Dev/Testing.
/// </summary>
public sealed class AddAnalyticsModuleGuardTests
{
    private const string ConnectionString = "Server=.;Database=test;Trusted_Connection=True;Encrypt=False";

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddAnalyticsModule_RejectsABlankMasterSecret_OutsideDevOrTesting(string environment)
    {
        var services = new ServiceCollection();

        var act = () => services.AddAnalyticsModule(
            ConnectionString,
            environment,
            options => options.MasterSecret = string.Empty);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SaltMasterSecret*");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddAnalyticsModule_Accepts_ANonBlankMasterSecret_OutsideDevOrTesting(string environment)
    {
        var services = new ServiceCollection();

        var act = () => services.AddAnalyticsModule(
            ConnectionString,
            environment,
            options => options.MasterSecret = "a-real-production-secret");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void AddAnalyticsModule_Tolerates_ABlankMasterSecret_InDevOrTesting(string environment)
    {
        var services = new ServiceCollection();

        var act = () => services.AddAnalyticsModule(
            ConnectionString,
            environment,
            options => options.MasterSecret = string.Empty);

        act.Should().NotThrow();
    }
}
