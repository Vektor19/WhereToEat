using FluentAssertions;
using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Anonymization;
using WhereToEat.Analytics.Infrastructure.Anonymization;
using Xunit;

namespace WhereToEat.Analytics.Application.UnitTests;

/// <summary>
/// Proves the <see cref="Anonymizer"/> applies the retain / hash / drop rules at ingest (§8.1 /
/// invariant #11): retained dimensions survive verbatim, the actor id becomes a non-reversible hash,
/// the time is hour-truncated, the location is coarsened to a neighbourhood-grade geohash, and the raw
/// id / precise lat/lng are dropped (the produced aggregate cannot even hold them).
/// </summary>
public sealed class AnonymizerTests
{
    private static Anonymizer CreateAnonymizer(string salt = "test-salt", int precision = 5)
    {
        var options = new AnonymizerOptions { GeohashPrecision = precision, MasterSecret = "master" };
        return new Anonymizer(new FixedSaltProvider(salt), options);
    }

    [Fact]
    public void Anonymize_Retains_TheNonIdentifyingDimensions()
    {
        var restaurant = Guid.NewGuid();
        var category = Guid.NewGuid();
        var dish = Guid.NewGuid();

        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.CardOpen,
            RestaurantId = restaurant,
            CategoryIds = [category],
            DishIds = [dish],
            SortMode = "best",
            Filters = ["price", "rating"],
            Position = 3,
        });

        result.IsSuccess.Should().BeTrue();
        var @event = result.Value;
        @event.Kind.Should().Be(EventKind.CardOpen);
        @event.Dimensions.RestaurantId.Should().Be(restaurant);
        @event.Dimensions.CategoryIds.Should().ContainSingle().Which.Should().Be(category);
        @event.Dimensions.DishIds.Should().ContainSingle().Which.Should().Be(dish);
        @event.Dimensions.SortMode.Should().Be("best");
        @event.Dimensions.Filters.Should().ContainInOrder("price", "rating");
        @event.Dimensions.Position.Should().Be(3);
    }

    [Fact]
    public void Anonymize_Coarsens_TimeToTheHourBucket()
    {
        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.Impression,
            OccurredAtUtc = new DateTimeOffset(2026, 6, 15, 14, 37, 52, TimeSpan.Zero),
        });

        // Minute/second precision dropped — only the top of the hour survives.
        result.Value.OccurredAtHour.HourUtc.Should().Be(new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Anonymize_Hashes_TheActorId_NonReversibly()
    {
        const string rawUserId = "user-12345";

        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.Session,
            UserId = rawUserId,
        });

        var actor = result.Value.Actor;
        actor.Should().NotBeNull();

        // A 64-char hex SHA-256 digest, and the raw id does not appear anywhere in it (non-reversible).
        actor!.Digest.Should().HaveLength(HashedActorId.DigestLength);
        actor.Digest.Should().NotContain(rawUserId);
        actor.Digest.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Anonymize_SameRawId_SameSalt_ProducesTheSameHash()
    {
        var anonymizer = CreateAnonymizer(salt: "window-A");

        var a = anonymizer.Anonymize(new RawAnalyticsEvent { Kind = EventKind.View, UserId = "u1" }).Value;
        var b = anonymizer.Anonymize(new RawAnalyticsEvent { Kind = EventKind.View, UserId = "u1" }).Value;

        a.Actor.Should().Be(b.Actor);
    }

    [Fact]
    public void Anonymize_SameRawId_DifferentSalt_ProducesADifferentHash()
    {
        var windowA = CreateAnonymizer(salt: "window-A")
            .Anonymize(new RawAnalyticsEvent { Kind = EventKind.View, UserId = "u1" }).Value;
        var windowB = CreateAnonymizer(salt: "window-B")
            .Anonymize(new RawAnalyticsEvent { Kind = EventKind.View, UserId = "u1" }).Value;

        windowA.Actor.Should().NotBe(windowB.Actor);
    }

    [Fact]
    public void Anonymize_FallsBackToSessionId_WhenNoUserId()
    {
        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.Session,
            SessionId = "sess-1",
        });

        result.Value.Actor.Should().NotBeNull();
    }

    [Fact]
    public void Anonymize_Drops_TheRawId_WhenActorless()
    {
        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent { Kind = EventKind.Impression });

        // No id supplied -> no actor hash; nothing identifying is invented.
        result.Value.Actor.Should().BeNull();
    }

    [Fact]
    public void Anonymize_Coarsens_PreciseLatLng_ToANeighbourhoodGradeGeohash()
    {
        var result = CreateAnonymizer(precision: 5).Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.Search,
            // Kyiv centre, precise to many decimals.
            Latitude = 50.4501234,
            Longitude = 30.5234123,
        });

        var geohash = result.Value.CoarseLocation;
        geohash.Should().NotBeNull();
        // The stored value is coarse (<= the domain precision floor), not point-grade.
        geohash!.Precision.Should().Be(5);
        geohash.Precision.Should().BeLessThanOrEqualTo(Geohash.MaxPrecision);
    }

    [Fact]
    public void Anonymize_Drops_PreciseLatLng_WhenNoLocationSupplied()
    {
        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent { Kind = EventKind.Filter });

        result.Value.CoarseLocation.Should().BeNull();
    }

    [Fact]
    public void Anonymize_GeohashPrecision_IsClampedToTheDomainFloor()
    {
        // Asking for an over-precise (point-grade) geohash is clamped down to the privacy floor.
        var result = CreateAnonymizer(precision: 12).Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.Search,
            Latitude = 50.45,
            Longitude = 30.52,
        });

        result.Value.CoarseLocation!.Precision.Should().Be(Geohash.MaxPrecision);
    }

    [Fact]
    public void Anonymize_OutOfRangeCoordinate_ReturnsAValidationFailure()
    {
        var result = CreateAnonymizer().Anonymize(new RawAnalyticsEvent
        {
            Kind = EventKind.Search,
            Latitude = 999,
            Longitude = 0,
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Analytics.InvalidLocation");
    }

    /// <summary>A test salt provider returning a fixed salt — stands in for the rotation window.</summary>
    private sealed class FixedSaltProvider : ISaltProvider
    {
        public FixedSaltProvider(string salt) => CurrentSalt = salt;

        public string CurrentSalt { get; }
    }
}
