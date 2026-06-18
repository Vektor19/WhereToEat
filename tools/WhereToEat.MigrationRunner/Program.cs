using DbUp.Engine.Output;
using WhereToEat.BuildingBlocks.Migrations;

// Console migration runner (Step 4). Reads the SQL Server connection string from the first
// argument or the WHERETOEAT_DB_CONNECTION environment variable, applies the embedded
// db/migrations scripts via DbUp, and reports the outcome. Exit code 0 = success (including the
// idempotent "nothing to apply" no-op), 1 = a usage error or a failed migration.

string? connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("WHERETOEAT_DB_CONNECTION");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: WhereToEat.MigrationRunner <connection-string>");
    Console.Error.WriteLine(
        "   or: set WHERETOEAT_DB_CONNECTION and run with no arguments.");
    return 1;
}

Console.WriteLine("Applying WhereToEat catalog migrations…");

// Stream DbUp's per-script progress/warnings to the console so a mid-run failure names the
// script that failed (TraceUpgradeLog would also let a structured sink capture it if hosted).
MigrationResult result = MigrationRunner.Run(connectionString, new ConsoleUpgradeLog());

if (!result.Successful)
{
    Console.Error.WriteLine($"Migration failed: {result.ErrorMessage}");
    return 1;
}

if (result.ScriptsApplied.Count == 0)
{
    Console.WriteLine("Database already up to date — no scripts applied.");
}
else
{
    Console.WriteLine($"Applied {result.ScriptsApplied.Count} script(s):");
    foreach (var script in result.ScriptsApplied)
    {
        Console.WriteLine($"  - {script}");
    }
}

return 0;
