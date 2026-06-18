using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;

namespace WhereToEat.BuildingBlocks.Migrations;

/// <summary>
/// Applies the versioned SQL migration scripts (the schema-of-record under <c>db/migrations</c>,
/// embedded into this assembly) to a SQL Server database using <b>DbUp</b>. DbUp records each
/// applied script in a journal table, so a second run is a no-op — running this on host startup or
/// from the console tool is safe and idempotent.
///
/// Scripts are discovered by their embedded-resource name (prefix
/// <c>WhereToEat.Migrations.Scripts.</c>) and ordered by that name, which matches the numeric file
/// prefixes (<c>0000_</c>, <c>0001_</c>, …) so they apply in the intended order.
/// </summary>
public static class MigrationRunner
{
    /// <summary>The embedded-resource prefix every migration script lives under (see the csproj).</summary>
    public const string ScriptResourcePrefix = "WhereToEat.Migrations.Scripts.";

    /// <summary>
    /// Ensures the target database exists, then applies any pending scripts in order. Returns a
    /// <see cref="MigrationResult"/> rather than throwing so callers can branch on success; on
    /// failure the DbUp error message is surfaced.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string of the database to migrate.</param>
    /// <param name="log">
    /// Where DbUp writes per-script progress and warnings. Defaults to a no-op log for library/test
    /// callers that don't want chatter; the console runner injects a real log (e.g.
    /// <see cref="ConsoleUpgradeLog"/> / <see cref="TraceUpgradeLog"/>) so a mid-run failure shows
    /// which script failed.
    /// </param>
    public static MigrationResult Run(string connectionString, IUpgradeLog? log = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        log ??= new NoOpUpgradeLog();

        // Create the database itself if it isn't there yet (the journal/tables go inside it).
        EnsureDatabase.For.SqlDatabase(connectionString, log);

        var scriptAssembly = typeof(MigrationRunner).GetTypeInfo().Assembly;

        UpgradeEngine engine = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                scriptAssembly,
                resourceName => resourceName.StartsWith(ScriptResourcePrefix, StringComparison.Ordinal))
            .WithExecutionTimeout(TimeSpan.FromMinutes(2))
            .LogTo(log)
            .Build();

        DatabaseUpgradeResult result = engine.PerformUpgrade();

        if (!result.Successful)
        {
            return MigrationResult.Failure(result.Error?.Message ?? "Migration failed for an unknown reason.");
        }

        var applied = result.Scripts.Select(s => s.Name).ToList();
        return MigrationResult.Success(applied);
    }
}
