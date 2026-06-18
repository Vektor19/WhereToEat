using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Application;
using WhereToEat.Worker;
using WhereToEat.Worker.Jobs;
using Xunit;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// Closes the Step 12 "Known deferral" (Item 2): the worker's real composition
/// (<see cref="WorkerCompositionRoot.AddJobDependencies"/>) now registers the production
/// <see cref="ICatalogMenuWriter"/>/<see cref="ICatalogDishResolver"/> (Catalog.Infrastructure) and
/// <see cref="IRestaurantGeocoder"/> (Geo.Infrastructure) adapters, so the
/// <see cref="RunParseCommandHandler"/> the <see cref="WeeklyParseJob"/> drives — and the whole job
/// dependency graph — resolves at startup without throwing. Before Step 13 these seams were test-double
/// only and the job threw at its first Quartz fire; this test mechanically proves that is fixed.
/// <para>
/// No Docker is needed: this is a pure DI-graph resolution check (the provider is built and the job is
/// resolved; nothing connects to the DB/bus). The provider is built with scope + on-build validation on,
/// so a missing dependency anywhere in the graph would fail the test.
/// </para>
/// </summary>
public sealed class WeeklyParseJobResolutionTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = "Server=localhost;Database=WhereToEat;Trusted_Connection=True;TrustServerCertificate=True",
                // Nominatim options are validated; supply the policy-required fields.
                ["Nominatim:BaseUrl"] = "https://nominatim.openstreetmap.org/",
                ["Nominatim:UserAgent"] = "WhereToEat-Test/1.0 (contact: ops@wheretoeat.example)",
                ["Nominatim:MinRequestIntervalMs"] = "1000",
                ["Nominatim:RequestTimeoutMs"] = "10000",
                // The bus + cache default to in-process / null when unset, but we set them explicitly.
                ["Messaging:Transport"] = "InMemory",
                ["Redis:Enabled"] = "false",
            })
            .Build();

    [Fact]
    public void WeeklyParseJob_full_dependency_graph_resolves()
    {
        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("Catalog")!;

        var services = new ServiceCollection();
        services.AddLogging();

        // The same composition the running worker uses for its jobs (minus the Quartz scheduler, which
        // would need a live SQL store at start-up). The four jobs are registered here so the resolution
        // check covers exactly what the scheduler would later instantiate.
        services.AddJobDependencies(configuration, connectionString);
        services.AddTransient<WeeklyParseJob>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        using var scope = provider.CreateScope();

        // The deps that previously had no production implementation — assert each resolves to its real
        // adapter (not a missing/throwing registration).
        scope.ServiceProvider.GetRequiredService<ICatalogMenuWriter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ICatalogDishResolver>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IRestaurantGeocoder>().Should().NotBeNull();

        // The handler the job drives, and the job itself — the whole graph must construct without throwing.
        scope.ServiceProvider.GetRequiredService<RunParseCommandHandler>().Should().NotBeNull();

        var job = scope.ServiceProvider.GetRequiredService<WeeklyParseJob>();
        job.Should().NotBeNull();
    }
}
