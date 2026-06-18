using FluentAssertions;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Anonymization;
using Xunit;

namespace WhereToEat.Analytics.Domain.UnitTests;

/// <summary>
/// The retain/hash/drop anonymization shape (§8.1 / invariant #11), at the value-object level: a coarse
/// geohash cannot be point-precise, a hashed actor id refuses a raw-looking value, time is
/// hour-truncated, retained dimensions survive verbatim, and the event aggregate cannot represent any
/// dropped (raw-id / precise-coordinate / minute-grade) data.
/// </summary>
public sealed class AnonymizationValueObjectTests
{
    // ---- Geohash (coarsen location) -----------------------------------------------------------

    [Fact]
    public void Geohash_AcceptsANeighbourhoodGradeValue()
    {
        var result = Geohash.Create("u8vk3");

        result.IsSuccess.Should().BeTrue();
        result.Value.Precision.Should().Be(5);
    }

    [Fact]
    public void Geohash_RejectsAPointPreciseValue()
    {
        // Longer than the neighbourhood-grade cap — would identify a precise location.
        var result = Geohash.Create("u8vk3m9d7");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geohash.TooPrecise");
    }

    [Fact]
    public void Geohash_RejectsNonBase32Characters()
    {
        Geohash.Create("aio").IsFailure.Should().BeTrue("a, i, o are excluded from the geohash alphabet");
    }

    // ---- HashedActorId (hash identity) --------------------------------------------------------

    [Fact]
    public void HashedActorId_AcceptsA64CharHexDigest()
    {
        var digest = new string('a', 64);

        HashedActorId.FromDigest(digest).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void HashedActorId_RejectsARawLookingValue()
    {
        // A raw user id / guid is not a 64-char hex digest, so it can't masquerade as a hash.
        HashedActorId.FromDigest("user-42").IsFailure.Should().BeTrue();
        HashedActorId.FromDigest(Guid.NewGuid().ToString()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void HashedActorId_EqualityIsByValue()
    {
        var digest = new string('b', 64);

        HashedActorId.FromDigest(digest).Value.Should().Be(HashedActorId.FromDigest(digest).Value);
    }

    // ---- HourBucket (coarsen time) ------------------------------------------------------------

    [Fact]
    public void HourBucket_TruncatesMinutesAndSeconds_ToUtcHour()
    {
        var instant = new DateTimeOffset(2026, 6, 15, 14, 37, 51, TimeSpan.FromHours(3));

        var bucket = HourBucket.FromInstant(instant);

        // 14:37:51 +03:00 == 11:37:51 UTC -> truncated to 11:00:00 UTC.
        bucket.HourUtc.Should().Be(new DateTimeOffset(2026, 6, 15, 11, 0, 0, TimeSpan.Zero));
    }

    // ---- EventDimensions (retain) -------------------------------------------------------------

    [Fact]
    public void EventDimensions_RetainNonIdentifyingFieldsVerbatim()
    {
        var category = Guid.NewGuid();
        var dish = Guid.NewGuid();

        var dimensions = EventDimensions.Create(
            categoryIds: [category],
            dishIds: [dish],
            sortMode: "best",
            filters: ["price", "rating"],
            position: 3);

        dimensions.CategoryIds.Should().ContainSingle().Which.Should().Be(category);
        dimensions.DishIds.Should().ContainSingle().Which.Should().Be(dish);
        dimensions.SortMode.Should().Be("best");
        dimensions.Filters.Should().ContainInOrder("price", "rating");
        dimensions.Position.Should().Be(3);
    }

    [Fact]
    public void EventDimensions_NormalizeNullsToEmpty()
    {
        var dimensions = EventDimensions.Create();

        dimensions.CategoryIds.Should().BeEmpty();
        dimensions.DishIds.Should().BeEmpty();
        dimensions.SortMode.Should().BeNull();
        dimensions.Filters.Should().BeEmpty();
        dimensions.Position.Should().BeNull();
    }

    // ---- AnalyticsEvent (the assembled anonymized fact) ---------------------------------------

    [Fact]
    public void AnalyticsEvent_AssemblesFromTheAnonymizedPieces()
    {
        var @event = AnalyticsEvent.Record(
            EventKind.Impression,
            HourBucket.FromInstant(DateTimeOffset.UtcNow),
            EventDimensions.Create(position: 1),
            HashedActorId.FromDigest(new string('c', 64)).Value,
            Geohash.Create("u8vk3").Value).Value;

        @event.Kind.Should().Be(EventKind.Impression);
        @event.Actor.Should().NotBeNull();
        @event.CoarseLocation.Should().NotBeNull();
        @event.Dimensions.Position.Should().Be(1);
    }

    [Fact]
    public void AnalyticsEvent_AllowsActorlessAndLocationlessEvents()
    {
        var @event = AnalyticsEvent.Record(
            EventKind.Session,
            HourBucket.FromInstant(DateTimeOffset.UtcNow),
            EventDimensions.Create()).Value;

        @event.Actor.Should().BeNull();
        @event.CoarseLocation.Should().BeNull();
    }
}
