using FluentAssertions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Taxonomy;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Catalog.Domain.UnitTests.Taxonomy;

/// <summary>Two-level taxonomy invariant (CLAUDE.md #2): a Dish must belong to a Category.</summary>
public sealed class TaxonomyInvariantTests
{
    [Fact]
    public void CreateDish_WithoutCategory_Fails()
    {
        var result = Dish.Create(default, "Борщ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dish.CategoryRequired");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void CreateDish_WithCategory_Succeeds()
    {
        var categoryId = CategoryId.New();

        var result = Dish.Create(categoryId, "  Борщ  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryId.Should().Be(categoryId);
        result.Value.CanonicalName.Should().Be("Борщ");
    }

    [Fact]
    public void CreateDish_WithBlankName_Fails()
    {
        var result = Dish.Create(CategoryId.New(), "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dish.NameRequired");
    }

    [Fact]
    public void MoveToCategory_WithDefaultCategory_Fails()
    {
        var dish = Dish.Create(CategoryId.New(), "Борщ").Value;

        var result = dish.MoveToCategory(default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dish.CategoryRequired");
    }

    [Fact]
    public void MoveToCategory_WithRealCategory_Succeeds()
    {
        var dish = Dish.Create(CategoryId.New(), "Борщ").Value;
        var newCategory = CategoryId.New();

        var result = dish.MoveToCategory(newCategory);

        result.IsSuccess.Should().BeTrue();
        dish.CategoryId.Should().Be(newCategory);
    }

    [Fact]
    public void CreateCategory_WithBlankName_Fails()
    {
        var result = Category.Create("  ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Category.NameRequired");
    }
}
