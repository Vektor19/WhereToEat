using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// The shared fixture for the public-API integration tests: it starts one SQL Server container,
/// applies the DbUp migrations, then boots the real <see cref="Program"/> host in-process via
/// <see cref="WebApplicationFactory{TEntryPoint}"/> pointed at that container. A symmetric test
/// signing key is injected through this host's own <see cref="IConfiguration"/> (per-host
/// <c>UseSetting</c>, not a process-wide env var) so the host accepts test-minted JWTs — this lets
/// the 401/200 auth behaviour be asserted without a live IdP, while the seam stays scoped to this
/// host (no cross-host/process race). The host runs in the <c>Testing</c> environment, the only
/// place the auth wiring honours the test key. One container + one host per test class keeps the
/// (one-time) container cost off each test.
/// </summary>
public sealed class PublicApiFixture : IAsyncLifetime
{
    /// <summary>The symmetric key the test host trusts (HS256). Must be ≥ 32 bytes for HS256.</summary>
    public const string TestSigningKey = "wheretoeat-public-api-integration-test-signing-key-0123456789";

    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The booted host's factory; create clients from it.</summary>
    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialized.");

    /// <summary>Creates an HTTP client against the in-process host.</summary>
    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        ConnectionString = _container.GetConnectionString();

        var migration = MigrationRunner.Run(ConnectionString);
        if (!migration.Successful)
        {
            throw new InvalidOperationException($"Migrations failed in the test fixture: {migration.ErrorMessage}");
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            // UseSetting puts these at the top of THIS host's configuration so they win over the
            // host's own appsettings regardless of provider order — and stay scoped to this host
            // (no process-wide env var racing with parallel/multi-assembly hosts). The test signing
            // key drives token validation here; the auth wiring reads it from IConfiguration and
            // only honours it because this host runs in the Testing environment.
            builder.UseSetting("ConnectionStrings:Catalog", ConnectionString);
            builder.UseSetting("Authentication:Oidc:Authority", string.Empty);
            builder.UseSetting("Authentication:Oidc:Audience", string.Empty);
            builder.UseSetting("Authentication:Oidc:RequireHttpsMetadata", "false");
            builder.UseSetting(
                $"{OidcOptions.SectionName}:{AuthServiceCollectionExtensions.TestSigningKeyConfigKey}",
                TestSigningKey);
        });

        // Force the host to build now so any composition error surfaces in InitializeAsync.
        _ = _factory.Services;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>The xUnit collection so the (expensive) fixture is shared across the test classes.</summary>
[CollectionDefinition(Name)]
public sealed class PublicApiTestGroup : ICollectionFixture<PublicApiFixture>
{
    public const string Name = "PublicApi integration";
}
