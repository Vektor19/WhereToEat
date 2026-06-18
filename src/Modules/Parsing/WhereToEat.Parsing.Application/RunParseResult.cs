namespace WhereToEat.Parsing.Application;

/// <summary>
/// Why a parse run ended — distinct outcomes so the worker and the tests can assert exactly what
/// happened (a compliance block, an admin-protection skip, and a successful persist are different
/// things, not all "failure").
/// </summary>
public enum RunParseOutcome
{
    /// <summary>The menu was parsed and the cleared items were persisted (some may be quarantined).</summary>
    Persisted = 0,

    /// <summary>Blocked before any fetch by robots.txt (invariant #9).</summary>
    BlockedByRobots = 1,

    /// <summary>Blocked before any fetch by the per-host rate-limit (invariant #9).</summary>
    BlockedByRateLimit = 2,

    /// <summary>Rejected before any fetch — the host is not a first-party site (invariant #9).</summary>
    RejectedNotFirstParty = 3,

    /// <summary>Skipped entirely: the restaurant is flagged <c>DoNotUpdate</c> (invariant #3).</summary>
    SkippedDoNotUpdate = 4,

    /// <summary>The strategy fetch/parse failed (logged and skipped, not fatal to a batch).</summary>
    ParseFailed = 5,
}

/// <summary>
/// The result of a <see cref="RunParseCommand"/>: the <see cref="Outcome"/> plus the counts the tests
/// and observability care about — how many mappable items were persisted as live menu items, how many
/// raw items were quarantined (unmappable, invariant #2), and how many cleared items were skipped by
/// the per-item <c>DoNotParse</c> gate (invariant #3).
/// </summary>
public sealed record RunParseResult(
    RunParseOutcome Outcome,
    int PersistedItemCount = 0,
    int QuarantinedItemCount = 0,
    int DoNotParseSkippedCount = 0);
