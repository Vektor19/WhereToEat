using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain.Abstractions;
using WhereToEat.Analytics.Infrastructure.DependencyInjection;
using WhereToEat.BuildingBlocks.Persistence;
using Xunit;

namespace WhereToEat.Analytics.Application.UnitTests;

/// <summary>
/// Proves the Step 23 slim read registration
/// <see cref="AnalyticsInfrastructureServiceCollectionExtensions.AddAnalyticsReadModule"/>: it registers
/// the aggregates-only <see cref="IAnalyticsRollupReader"/> + the shared connection factory so the
/// dashboard-reading host (the admin host) can resolve the rollup reader, and it wires <b>none</b> of
/// the ingest/anonymizer stack (writer, salt provider, anonymizer, ingest handler) — the admin host
/// never ingests events. A blank connection string is rejected at composition time.
/// </summary>
public sealed class AddAnalyticsReadModuleTests
{
    private const string ConnectionString = "Server=.;Database=test;Trusted_Connection=True;Encrypt=False";

    [Fact]
    public void Registers_TheRollupReader_AndConnectionFactory()
    {
        var services = new ServiceCollection();

        services.AddAnalyticsReadModule(ConnectionString);
        using var provider = services.BuildServiceProvider();

        provider.GetService<IAnalyticsRollupReader>().Should().NotBeNull();
        provider.GetService<ISqlConnectionFactory>().Should().NotBeNull();
    }

    [Fact]
    public void DoesNotRegister_TheIngestStack()
    {
        var services = new ServiceCollection();

        services.AddAnalyticsReadModule(ConnectionString);

        // The read-only host needs neither the writer, the anonymizer, nor the ingest handler — by NOT
        // registering them the admin host's surface stays read-only (it cannot accidentally ingest).
        var serviceTypes = services.Select(d => d.ServiceType).ToList();
        serviceTypes.Should().NotContain(typeof(IAnalyticsEventStore));
        serviceTypes.Should().NotContain(typeof(IAnonymizer));
        serviceTypes.Should().NotContain(typeof(ISaltProvider));
        serviceTypes.Should().NotContain(typeof(IngestEventCommandHandler));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_ABlankConnectionString(string connectionString)
    {
        var services = new ServiceCollection();

        var act = () => services.AddAnalyticsReadModule(connectionString);

        act.Should().Throw<ArgumentException>();
    }
}
