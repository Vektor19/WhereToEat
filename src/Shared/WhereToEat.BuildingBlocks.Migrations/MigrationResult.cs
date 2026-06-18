namespace WhereToEat.BuildingBlocks.Migrations;

/// <summary>
/// The outcome of a migration run: whether it succeeded, how many scripts were applied this run
/// (0 when everything was already up to date — the idempotent no-op case), and the error message
/// when it failed. A plain value so callers (console host, integration tests) can assert without a
/// DbUp dependency leaking out of this library.
/// </summary>
public sealed record MigrationResult(bool Successful, IReadOnlyList<string> ScriptsApplied, string? ErrorMessage)
{
    /// <summary>A successful run that applied the given (possibly empty) set of scripts.</summary>
    public static MigrationResult Success(IReadOnlyList<string> scriptsApplied)
        => new(true, scriptsApplied, ErrorMessage: null);

    /// <summary>A failed run carrying the error message.</summary>
    public static MigrationResult Failure(string errorMessage)
        => new(false, Array.Empty<string>(), errorMessage);
}
