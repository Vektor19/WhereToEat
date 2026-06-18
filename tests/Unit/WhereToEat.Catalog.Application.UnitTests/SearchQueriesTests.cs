using FluentAssertions;
using NSubstitute;
using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Taxonomy;
using WhereToEat.Catalog.Search.Application;
using Xunit;

namespace WhereToEat.Catalog.Application.UnitTests;

/// <summary>
/// Unit tests for the Catalog.Search prefix-lookup handlers (invariant #1: deterministic prefix
/// filter, no NLP). They forward a trimmed prefix to the query-side read port, return an empty list
/// for a blank prefix without touching the port, and clamp the limit to the configured bounds.
/// </summary>
public sealed class SearchQueriesTests
{
    private readonly ICatalogReadPort _readPort = Substitute.For<ICatalogReadPort>();

    [Fact]
    public async Task SearchCategories_ForwardsTrimmedPrefix_AndMapsDtos()
    {
        var category = Category.Create(CategoryId.New(), "Перші страви").Value;
        _readPort.SearchCategoriesByPrefixAsync("Перш", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { category });

        var query = new SearchCategoriesQuery(_readPort);
        var result = await query.ExecuteAsync("  Перш  ");

        result.Should().ContainSingle(c => c.Id == category.Id.Value);
        await _readPort.Received(1)
            .SearchCategoriesByPrefixAsync("Перш", SearchCategoriesQuery.DefaultLimit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchDishes_BlankPrefix_ReturnsEmpty_WithoutHittingReadPort()
    {
        var query = new SearchDishesQuery(_readPort);

        var result = await query.ExecuteAsync("   ");

        result.Should().BeEmpty();
        await _readPort.DidNotReceive()
            .SearchDishesByPrefixAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchDishes_ClampsLimit_ToMax()
    {
        var dish = Dish.Create(DishId.New(), CategoryId.New(), "Борщ").Value;
        _readPort.SearchDishesByPrefixAsync("Бор", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { dish });

        var query = new SearchDishesQuery(_readPort);
        await query.ExecuteAsync("Бор", limit: 10_000);

        await _readPort.Received(1)
            .SearchDishesByPrefixAsync("Бор", SearchDishesQuery.MaxLimit, Arg.Any<CancellationToken>());
    }
}
