using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Parsing.UnitTests;

/// <summary>
/// The normalizer's mapped/unmapped split (invariant #2): a raw dish the resolver maps to a canonical
/// dish becomes a <see cref="NormalizedMenuItem"/>; a raw dish the resolver returns <c>null</c> for is
/// routed to <see cref="ParseQuarantineItem"/> — never dropped, never throwing/failing. A menu with
/// both mappable and unmappable dishes yields a non-empty result in both buckets.
/// </summary>
public sealed class NormalizerTests
{
    private static Money Uah(decimal amount) => Money.Create(amount, "UAH").Value;

    private static ParsedMenu MenuOf(params ParsedDish[] dishes)
        => new(
            new ParsedRestaurantFacts("Борщ Кафе", "вул. Хрещатик, 1", "Київ", Array.Empty<ParsedContactLink>()),
            dishes);

    [Fact]
    public async Task MappableAndUnmappable_AreSplit_NotDropped()
    {
        var resolver = Substitute.For<ICatalogDishResolver>();
        var borschId = Guid.NewGuid();
        var varenykyId = Guid.NewGuid();

        resolver.ResolveAsync("Борщ", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CanonicalDishDto(borschId, Guid.NewGuid()));
        resolver.ResolveAsync("Вареники", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CanonicalDishDto(varenykyId, Guid.NewGuid()));
        // "Узвар" has no canonical match -> unmappable.
        resolver.ResolveAsync("Узвар", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((CanonicalDishDto?)null);

        var time = new FakeTimeProvider();
        var normalizer = new Normalizer(resolver, time, NullLogger<Normalizer>.Instance);

        var menu = MenuOf(
            new ParsedDish("Борщ", Uah(95m), "350 г", "Перші страви"),
            new ParsedDish("Вареники", Uah(120m), "250 г", "Другі страви"),
            new ParsedDish("Узвар", Uah(45m), "300 мл", "Напої"));

        var result = await normalizer.NormalizeAsync(menu);

        result.MappedItems.Should().HaveCount(2);
        result.MappedItems.Select(m => m.DishId).Should().BeEquivalentTo(new[] { borschId, varenykyId });

        result.Quarantined.Should().ContainSingle();
        var quarantined = result.Quarantined[0];
        quarantined.RawName.Should().Be("Узвар");
        quarantined.RestaurantName.Should().Be("Борщ Кафе");
        quarantined.Price.Should().Be(Uah(45m));
        quarantined.DetectedAtUtc.Should().Be(time.GetUtcNow());
    }

    [Fact]
    public async Task AllUnmappable_StillSucceeds_AllQuarantined()
    {
        var resolver = Substitute.For<ICatalogDishResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((CanonicalDishDto?)null);

        var normalizer = new Normalizer(resolver, new FakeTimeProvider(), NullLogger<Normalizer>.Instance);

        var menu = MenuOf(new ParsedDish("Невідома страва", Uah(10m), null, null));

        var result = await normalizer.NormalizeAsync(menu);

        result.MappedItems.Should().BeEmpty();
        result.Quarantined.Should().ContainSingle("an all-unmappable menu does not fail — it all quarantines");
    }
}
