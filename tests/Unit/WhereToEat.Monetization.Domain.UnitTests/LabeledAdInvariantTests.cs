using System.Reflection;
using FluentAssertions;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Identifiers;
using Xunit;

namespace WhereToEat.Monetization.Domain.UnitTests;

/// <summary>
/// Invariant #10: paid placements are <b>always labeled</b> and <b>separate from organic ranking</b> —
/// organic ranking is not for sale. The labeled-ad marker is verified directly; the "no organic-rank
/// field" half is verified by a reflection scan of the type's public surface, so a future property
/// that could feed ranking would fail this test loudly.
/// </summary>
public sealed class LabeledAdInvariantTests
{
    private static readonly VenueRef Venue = VenueRef.From(Guid.NewGuid());
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset End = DateTimeOffset.UtcNow.AddDays(30);

    // Tokens that would indicate an organic-ranking input. No public member of AdPlacement/Promotion
    // may contain any of these — that is the compile-time-verifiable shape of "ads don't rank".
    private static readonly string[] RankingTokens =
    [
        "rank", "boost", "weight", "priority", "score", "relevance", "organic", "position", "sortorder",
    ];

    [Fact]
    public void AdPlacement_IsAlwaysLabeled()
    {
        var placement = AdPlacement.Create(Venue, "pizza:kyiv", Start, End).Value;

        placement.IsLabeledAd.Should().BeTrue();
    }

    [Fact]
    public void Promotion_IsAlwaysLabeled()
    {
        var promotion = Promotion.Create(Venue, "-20% on the Margherita", Start, End).Value;

        promotion.IsLabeledAd.Should().BeTrue();
    }

    [Fact]
    public void AdPlacement_RejectsInvalidWindow()
    {
        AdPlacement.Create(Venue, "pizza:kyiv", End, Start).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdPlacement_ActiveOnlyWithinWindow()
    {
        var placement = AdPlacement.Create(Venue, "pizza:kyiv", Start, End).Value;

        placement.IsActiveAt(Start.AddDays(1)).Should().BeTrue();
        placement.IsActiveAt(End.AddDays(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(AdPlacement))]
    [InlineData(typeof(Promotion))]
    public void PaidPlacement_HasNoPublicFieldThatCouldFeedOrganicRanking(Type type)
    {
        var rankingMembers = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m is PropertyInfo or FieldInfo)
            .Where(m => RankingTokens.Any(token =>
                m.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Name)
            .ToList();

        rankingMembers.Should().BeEmpty(
            $"{type.Name} is a labeled ad slot and must expose NO organic-ranking input (invariant #10)");
    }
}
