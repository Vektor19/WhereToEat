using Xunit;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// Shares ONE migrated SQL Server container across all worker integration test classes (the median,
/// rating-recompute, and clustering tests). xUnit runs the classes in a collection serially, which also
/// keeps the clustering test (which schedules into the shared store) from racing the data-seeding tests
/// — and one container instead of three avoids the known Testcontainers parallel-startup port flake.
/// </summary>
[CollectionDefinition("worker-sql")]
public sealed class WorkerSqlCollectionDefinition : ICollectionFixture<WorkerSqlServerFixture>;
