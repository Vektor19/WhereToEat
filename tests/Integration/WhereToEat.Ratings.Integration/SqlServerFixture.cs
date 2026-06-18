using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.Ratings.Integration;

/// <summary>
/// A SQL Server Testcontainers fixture (same pattern as the Catalog integration suite): it starts one
/// SQL Server container, applies the DbUp migrations (0000-0009, which now include 0004_create_ratings)
/// against it once, and tears the container down afterwards. Sharing one container across the class's
/// tests keeps the container-start cost off every test while giving the class a clean, migrated database.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The scripts the migration run applied (asserted by the migration test).</summary>
    public IReadOnlyList<string> AppliedScripts { get; private set; } = Array.Empty<string>();

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

        AppliedScripts = result.ScriptsApplied;
    }

    /// <summary>Disposes the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
