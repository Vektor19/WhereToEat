using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Infrastructure;

/// <summary>
/// A <b>startup guard that fails fast</b> if EF Core would ever try to own or alter the schema. The
/// schema-of-record is the DbUp SQL scripts (Steps 4–5); EF maps database-first and must NEVER create
/// or migrate tables (the design's "EF migrations disabled" decision). The guard asserts two things at
/// startup, before any admin request is served:
/// <list type="number">
///   <item><b>No EF migrations are defined</b> for <see cref="AdminDbContext"/> — a non-empty
///   migrations assembly would mean someone added a <c>Migrations</c> folder, which is forbidden.</item>
///   <item><b>Every table EF maps already exists</b> in the live database (the scripts created it). If
///   a mapped table is missing, EF would otherwise silently try to create it on first use — the guard
///   turns that into an immediate, explicit failure instead.</item>
/// </list>
/// The guard NEVER calls <c>EnsureCreated</c>/<c>Migrate</c> and makes no schema change itself; it only
/// reads metadata + <c>INFORMATION_SCHEMA</c>.
/// </summary>
public static class EfMigrationsDisabledGuard
{
    /// <summary>
    /// Runs the guard against <paramref name="context"/>. Returns a failure <see cref="Result"/>
    /// (rather than throwing) so the caller can decide how to surface it; the host wrapper
    /// <see cref="EnsureNoSchemaDriftOrThrow"/> throws to fail startup fast.
    /// </summary>
    public static async Task<Result> EnsureNoSchemaDriftAsync(
        AdminDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // (1) No EF migrations may be defined — a Migrations folder is forbidden by the design.
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        if (migrationsAssembly.Migrations.Count > 0)
        {
            return Result.Failure(Error.Failure(
                "AdminDbContext.MigrationsForbidden",
                "EF migrations are disabled for the Admin module — the SQL scripts are the " +
                $"schema-of-record. Found {migrationsAssembly.Migrations.Count} EF migration(s); " +
                "remove the Migrations folder."));
        }

        // (2) Every table EF maps must already exist in the live DB (the scripts created it), so EF
        //     never tries to create one. Read INFORMATION_SCHEMA; never alter the schema.
        var mappedTables = context.Model.GetEntityTypes()
            .Select(e => (Schema: e.GetSchema() ?? "dbo", Table: e.GetTableName()))
            .Where(t => t.Table is not null)
            .Distinct()
            .ToList();

        foreach (var (schema, table) in mappedTables)
        {
            var exists = await TableExistsAsync(context, schema, table!, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                return Result.Failure(Error.Failure(
                    "AdminDbContext.MissingTable",
                    $"EF maps table [{schema}].[{table}] but it does not exist in the database. The " +
                    "DbUp SQL scripts own the schema; EF must not create it. Apply the migrations " +
                    "before starting the admin host."));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Host convenience: runs <see cref="EnsureNoSchemaDriftAsync"/> and throws on failure so a
    /// misconfigured admin host fails fast at startup rather than serving requests against a schema EF
    /// might mutate.
    /// </summary>
    public static async Task EnsureNoSchemaDriftOrThrow(
        AdminDbContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await EnsureNoSchemaDriftAsync(context, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"EF migrations-disabled guard failed ({result.Error.Code}): {result.Error.Message}");
        }
    }

    private static async Task<bool> TableExistsAsync(
        AdminDbContext context,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var opened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            opened = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;";

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (opened)
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }
}
