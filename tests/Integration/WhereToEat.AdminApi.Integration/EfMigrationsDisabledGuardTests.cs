using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhereToEat.Admin.Infrastructure;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// The EF-migrations-disabled startup guard (Step 10 acceptance): EF maps database-first onto the
/// script-owned schema and must NEVER own/alter it. The guard passes against the real migrated schema
/// (no EF migrations are defined and every mapped table exists), and FAILS FAST when a mapped table is
/// missing — proving EF would otherwise have tried a schema change. The guard reads only metadata +
/// INFORMATION_SCHEMA; it never calls EnsureCreated/Migrate, so it makes no schema change itself.
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class EfMigrationsDisabledGuardTests
{
    private readonly AdminApiFixture _fixture;

    public EfMigrationsDisabledGuardTests(AdminApiFixture fixture) => _fixture = fixture;

    private static AdminDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AdminDbContext).Assembly.GetName().Name))
            .Options;
        return new AdminDbContext(options);
    }

    [Fact]
    public async Task Guard_Passes_AgainstTheScriptCreatedSchema()
    {
        await using var context = NewContext(_fixture.ConnectionString);

        var result = await EfMigrationsDisabledGuard.EnsureNoSchemaDriftAsync(context);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "the script schema matches the EF model");
    }

    [Fact]
    public async Task NoEfMigrations_AreDefined_ForTheAdminContext()
    {
        await using var context = NewContext(_fixture.ConnectionString);

        // No Migrations folder exists in Admin.Infrastructure; the assembly carries zero EF migrations.
        var migrations = context.Database.GetMigrations();

        migrations.Should().BeEmpty("the SQL scripts are the schema-of-record; EF migrations are disabled");
    }

    [Fact]
    public async Task Guard_FailsFast_WhenAMappedTableIsMissing()
    {
        // Point the context at tempdb — an always-present database that has NONE of the catalog/admin
        // tables (the migrations created those in the fixture's own database). The guard must report
        // the missing table rather than letting EF silently try to create it.
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = "tempdb",
        };

        await using var context = NewContext(builder.ConnectionString);

        var result = await EfMigrationsDisabledGuard.EnsureNoSchemaDriftAsync(context);

        result.IsFailure.Should().BeTrue("tempdb has none of the mapped tables, so EF would attempt a schema change");
        result.Error.Code.Should().Be("AdminDbContext.MissingTable");
    }
}
