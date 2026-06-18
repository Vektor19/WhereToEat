namespace WhereToEat.Parsing.Application.Compliance;

/// <summary>
/// The first-party allow-list gate (invariant #9 — first-party sites only, never aggregators). Asked,
/// <b>before any fetch</b>, whether a host is a permitted first-party restaurant site. A non-first-party
/// host (an aggregator/marketplace) is rejected outright — we never parse aggregators. Implementations
/// match against a configured allow-list of permitted hosts and/or a deny-list of known aggregators.
/// </summary>
public interface IFirstPartyAllowList
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="host"/> is an allowed first-party source, <c>false</c>
    /// when it is not (e.g. a known aggregator), in which case the parse is rejected before any fetch.
    /// </summary>
    bool IsFirstParty(string host);
}
