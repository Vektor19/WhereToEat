using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// A SQL Server Testcontainers fixture for the worker integration tests: it starts one SQL Server
/// container, applies the DbUp migrations (which now include 0013's Quartz cluster tables and Step 7's
/// 0010 price-median table) once, and tears it down afterwards. Sharing one migrated database across a
/// test class keeps the container start cost off every test.
/// </summary>
public sealed class WorkerSqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Starts the container and applies the schema-of-record migrations.</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        ConnectionString = _container.GetConnectionString();

        var result = MigrationRunner.Run(ConnectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migrations failed in the worker test fixture: {result.ErrorMessage}");
        }
    }

    /// <summary>Disposes the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
