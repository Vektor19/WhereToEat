namespace WhereToEat.Parsing.Application.Compliance;

/// <summary>
/// The robots.txt compliance gate (invariant #9 — respect <c>robots.txt</c>). Asked, <b>before any
/// fetch</b>, whether our parser is allowed to fetch a given URL under the host's <c>robots.txt</c>.
/// A disallow blocks the parse for that source. Implementations fetch and cache the host's
/// <c>robots.txt</c> and evaluate it against our User-Agent.
/// </summary>
public interface IRobotsTxtGate
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="url"/> is allowed to be fetched per the host's
    /// <c>robots.txt</c>, <c>false</c> when it is disallowed. A failure to retrieve robots.txt is
    /// treated conservatively by the implementation (documented there).
    /// </summary>
    Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken = default);
}
