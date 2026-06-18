using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.Parsing.Integration;

/// <summary>
/// A SQL Server Testcontainers fixture (same pattern as the other module integration suites): it starts
/// one SQL Server container, applies the DbUp migrations (0000-0011, which now include
/// 0011_create_parse_quarantine) once, and tears the container down afterwards. The quarantine
/// integration test reads/writes <c>parsing.ParseQuarantine</c> against this migrated database.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The scripts the migration run applied (asserted by the test).</summary>
    public IReadOnlyList<string> AppliedScripts { get; private set; } = Array.Empty<string>();

    /// <summary>Starts the container and applies the schema-of-record migrations.</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        ConnectionString = _container.GetConnectionString();

        var result = MigrationRunner.Run(ConnectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migrations failed in the test fixture: {result.ErrorMessage}");
        }

        AppliedScripts = result.ScriptsApplied;
    }

    /// <summary>Disposes the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
