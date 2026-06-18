using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// The shared fixture for the admin-API integration tests: it starts one SQL Server container,
/// applies the DbUp migrations (the schema-of-record — EF maps onto it database-first), then boots the
/// real admin <see cref="Program"/> host in-process via <see cref="WebApplicationFactory{TEntryPoint}"/>
/// pointed at that container. A symmetric test signing key is injected through the host's own
/// configuration (per-host <c>UseSetting</c>, not a process-wide env var) so the host accepts
/// test-minted JWTs — the same Dev/Testing-only seam the public host uses — letting the
/// admin/user/anonymous authorization behaviour be asserted without a live IdP. The host runs in the
/// <c>Testing</c> environment, the only place the auth wiring honours the test key.
/// </summary>
public sealed class AdminApiFixture : IAsyncLifetime
{
    /// <summary>The symmetric key the test host trusts (HS256). Must be ≥ 32 bytes for HS256.</summary>
    public const string TestSigningKey = "wheretoeat-admin-api-integration-test-signing-key-0123456789";

    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The booted host's factory; create clients from it.</summary>
    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialized.");

    /// <summary>Creates an HTTP client against the in-process admin host.</summary>
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

            builder.UseSetting("ConnectionStrings:Catalog", ConnectionString);
            builder.UseSetting("Authentication:Oidc:Authority", string.Empty);
            builder.UseSetting("Authentication:Oidc:Audience", string.Empty);
            builder.UseSetting("Authentication:Oidc:RequireHttpsMetadata", "false");
            builder.UseSetting(
                $"{OidcOptions.SectionName}:{AuthServiceCollectionExtensions.TestSigningKeyConfigKey}",
                TestSigningKey);
        });

        // Touching Services makes WebApplicationFactory build AND start the in-process server now, which
        // runs the IStartupFilter pipeline — so the EF-migrations-disabled guard (registered as an
        // IStartupFilter in Program.cs) executes eagerly here. Any composition error or schema drift
        // surfaces in InitializeAsync rather than mid-test on the first HTTP request.
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
public sealed class AdminApiTestGroup : ICollectionFixture<AdminApiFixture>
{
    public const string Name = "AdminApi integration";
}
