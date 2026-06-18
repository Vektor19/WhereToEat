using FluentAssertions;
using WhereToEat.Analytics.Infrastructure.Anonymization;
using Xunit;

namespace WhereToEat.Analytics.Application.UnitTests;

/// <summary>
/// Proves the <see cref="RotatingSaltProvider"/> rotates the salt on schedule (§8.1 / invariant #11):
/// the salt is <b>stable within a rotation window</b> (so intra-window funnel math works) and
/// <b>different across windows</b> (so the same user id, salted+hashed, is not cross-window linkable).
/// </summary>
public sealed class RotatingSaltProviderTests
{
    private static RotatingSaltProvider Create(TimeSpan window, DateTimeOffset now)
        => new(new AnonymizerOptions { MasterSecret = "master", SaltRotationWindow = window }, () => now);

    [Fact]
    public void Salt_IsStable_WithinTheSameWindow()
    {
        var window = TimeSpan.FromDays(1);
        var early = new DateTimeOffset(2026, 6, 15, 0, 5, 0, TimeSpan.Zero);
        var late = new DateTimeOffset(2026, 6, 15, 23, 55, 0, TimeSpan.Zero);

        var saltEarly = Create(window, early).CurrentSalt;
        var saltLate = Create(window, late).CurrentSalt;

        // Same day -> same window -> identical salt.
        saltLate.Should().Be(saltEarly);
    }

    [Fact]
    public void Salt_Diverges_AcrossWindows()
    {
        var window = TimeSpan.FromDays(1);
        var day1 = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

        var salt1 = Create(window, day1).CurrentSalt;
        var salt2 = Create(window, day2).CurrentSalt;

        // Next window -> a different salt, so the same id hashes differently after rotation.
        salt2.Should().NotBe(salt1);
    }

    [Fact]
    public void Salt_RepeatedCalls_AtTheSameInstant_AreIdentical()
    {
        var provider = Create(TimeSpan.FromHours(1), new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero));

        provider.CurrentSalt.Should().Be(provider.CurrentSalt);
    }

    [Fact]
    public void Constructor_RejectsANonPositiveWindow()
    {
        var act = () => new RotatingSaltProvider(new AnonymizerOptions { SaltRotationWindow = TimeSpan.Zero });

        act.Should().Throw<ArgumentException>();
    }
}
