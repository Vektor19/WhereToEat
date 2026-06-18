using Microsoft.Extensions.Options;
using WhereToEat.Parsing.Application.Compliance;

namespace WhereToEat.Parsing.Infrastructure.Compliance;

/// <summary>
/// Options for the first-party allow-list: the explicit list of permitted first-party hosts and a
/// deny-list of known aggregator hosts (invariant #9 — first-party only, never aggregators).
/// </summary>
public sealed class FirstPartyAllowListOptions
{
    /// <summary>The appsettings section these options bind to.</summary>
    public const string SectionName = "Parsing:FirstParty";

    /// <summary>Hosts explicitly permitted as first-party restaurant sites (case-insensitive).</summary>
    public IList<string> AllowedHosts { get; } = new List<string>();

    /// <summary>Known aggregator/marketplace hosts that are always rejected (case-insensitive).</summary>
    public IList<string> DeniedHosts { get; } = new List<string>();
}

/// <summary>
/// The first-party allow-list gate (invariant #9): a host is first-party only when it is on the
/// configured <see cref="FirstPartyAllowListOptions.AllowedHosts"/> and not on the
/// <see cref="FirstPartyAllowListOptions.DeniedHosts"/> aggregator list. The deny-list wins on a
/// conflict — a host that is somehow on both is treated as an aggregator and rejected. Matching is
/// host-suffix aware so <c>menu.borsch-cafe.example</c> matches an allowed <c>borsch-cafe.example</c>.
/// </summary>
public sealed class FirstPartyAllowList : IFirstPartyAllowList
{
    private readonly FirstPartyAllowListOptions _options;

    public FirstPartyAllowList(IOptions<FirstPartyAllowListOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool IsFirstParty(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (_options.DeniedHosts.Any(denied => HostMatches(host, denied)))
        {
            return false;
        }

        return _options.AllowedHosts.Any(allowed => HostMatches(host, allowed));
    }

    // A host matches a configured entry when it equals it or is a sub-domain of it.
    private static bool HostMatches(string host, string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        return host.Equals(entry, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase);
    }
}
