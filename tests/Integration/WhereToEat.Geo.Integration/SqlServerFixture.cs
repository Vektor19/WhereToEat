using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.Geo.Integration;

/// <summary>
/// A SQL Server Testcontainers fixture: starts one container, applies the DbUp migrations (so the
/// catalog schema incl. the geography coordinate column exists), and tears it down afterwards. Shared
/// across the geocode-persistence test class.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Starts the container and applies the migrations (the schema-of-record scripts).</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        ConnectionString = _container.GetConnectionString();

        var result = MigrationRunner.Run(ConnectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migrations failed in the test fixture: {result.ErrorMessage}");
        }
    }

    /// <summary>Disposes the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
