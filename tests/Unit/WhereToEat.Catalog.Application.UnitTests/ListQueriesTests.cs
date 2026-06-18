using FluentAssertions;
using NSubstitute;
using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Application.Queries;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Taxonomy;
using Xunit;

namespace WhereToEat.Catalog.Application.UnitTests;

/// <summary>
/// Unit tests for the list queries with a mocked read port: they map domain objects to flat DTOs
/// and pass the category id through unchanged.
/// </summary>
public sealed class ListQueriesTests
{
    private readonly ICatalogReadPort _readPort = Substitute.For<ICatalogReadPort>();

    [Fact]
    public async Task ListCategories_MapsDomainToDtos()
    {
        var category = Category.Create(CategoryId.New(), "Перші страви").Value;
        _readPort.ListCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { category });

        var query = new ListCategoriesQuery(_readPort);
        var result = await query.ExecuteAsync();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(category.Id.Value);
        result[0].Name.Should().Be("Перші страви");
    }

    [Fact]
    public async Task ListDishesByCategory_PassesCategoryId_AndMapsDtos()
    {
        var categoryId = CategoryId.New();
        var dish = Dish.Create(DishId.New(), categoryId, "Борщ").Value;
        _readPort.ListDishesByCategoryAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(new[] { dish });

        var query = new ListDishesByCategoryQuery(_readPort);
        var result = await query.ExecuteAsync(categoryId.Value);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(dish.Id.Value);
        result[0].CategoryId.Should().Be(categoryId.Value);
        result[0].CanonicalName.Should().Be("Борщ");
    }
}
