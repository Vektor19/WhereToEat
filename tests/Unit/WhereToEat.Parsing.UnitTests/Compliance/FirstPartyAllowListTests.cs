using FluentAssertions;
using Microsoft.Extensions.Options;
using WhereToEat.Parsing.Infrastructure.Compliance;
using Xunit;

namespace WhereToEat.Parsing.UnitTests.Compliance;

/// <summary>
/// The first-party allow-list gate (invariant #9 — first-party only, never aggregators). It rejects a
/// host that is not explicitly allowed and any host on the aggregator deny-list (deny wins on a
/// conflict), and matches sub-domains of an allowed host.
/// </summary>
public sealed class FirstPartyAllowListTests
{
    private static FirstPartyAllowList Build(string? allowed = null, string? denied = null)
    {
        var options = new FirstPartyAllowListOptions();
        if (allowed is not null)
        {
            options.AllowedHosts.Add(allowed);
        }

        if (denied is not null)
        {
            options.DeniedHosts.Add(denied);
        }

        return new FirstPartyAllowList(Options.Create(options));
    }

    [Fact]
    public void AllowedHost_IsFirstParty()
    {
        var gate = Build(allowed: "borsch-cafe.example");

        gate.IsFirstParty("borsch-cafe.example").Should().BeTrue();
    }

    [Fact]
    public void Subdomain_OfAllowedHost_IsFirstParty()
    {
        var gate = Build(allowed: "borsch-cafe.example");

        gate.IsFirstParty("menu.borsch-cafe.example").Should().BeTrue();
    }

    [Fact]
    public void UnknownHost_IsRejected()
    {
        var gate = Build(allowed: "borsch-cafe.example");

        gate.IsFirstParty("some-aggregator.example").Should().BeFalse();
    }

    [Fact]
    public void AggregatorHost_OnDenyList_IsRejected_EvenIfAlsoAllowed()
    {
        // Deny wins: a host on both lists is treated as an aggregator (invariant #9).
        var gate = Build(allowed: "aggregator.example", denied: "aggregator.example");

        gate.IsFirstParty("aggregator.example").Should().BeFalse();
    }

    [Fact]
    public void BlankHost_IsRejected()
    {
        var gate = Build(allowed: "borsch-cafe.example");

        gate.IsFirstParty("").Should().BeFalse();
    }
}
