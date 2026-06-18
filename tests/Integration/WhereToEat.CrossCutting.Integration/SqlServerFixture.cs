using Testcontainers.MsSql;
using WhereToEat.BuildingBlocks.Migrations;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// A SQL Server Testcontainers fixture for the caching-decorator test: starts one SQL Server container,
/// applies the schema-of-record migrations (including Step 7's <c>recommendation.PriceMedian</c> table),
/// and tears it down afterwards.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>The connection string to the migrated database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        ConnectionString = _container.GetConnectionString();

        var result = MigrationRunner.Run(ConnectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migrations failed in the cross-cutting test fixture: {result.ErrorMessage}");
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>Shares one migrated SQL container across the SQL-backed cross-cutting tests.</summary>
[CollectionDefinition("crosscutting-sql")]
public sealed class CrossCuttingSqlCollectionDefinition : ICollectionFixture<SqlServerFixture>;
