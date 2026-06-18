using FluentAssertions;
using WhereToEat.Contracts.Recommendation;
using Xunit;

namespace WhereToEat.Contracts.UnitTests.Recommendation;

public sealed class SelectedItemTests
{
    [Fact]
    public void Category_PopulatesOnlyCategoryId()
    {
        var id = Guid.NewGuid();

        var item = SelectedItem.Category(id);

        item.CategoryId.Should().Be(id);
        item.DishId.Should().BeNull();
    }

    [Fact]
    public void Dish_PopulatesOnlyDishId()
    {
        var id = Guid.NewGuid();

        var item = SelectedItem.Dish(id);

        item.DishId.Should().Be(id);
        item.CategoryId.Should().BeNull();
    }

    [Fact]
    public void Category_WithEmptyGuid_Throws()
    {
        var act = () => SelectedItem.Category(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("categoryId");
    }

    [Fact]
    public void Dish_WithEmptyGuid_Throws()
    {
        var act = () => SelectedItem.Dish(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("dishId");
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var id = Guid.NewGuid();

        SelectedItem.Category(id).Should().Be(SelectedItem.Category(id));
        SelectedItem.Dish(id).Should().Be(SelectedItem.Dish(id));

        // A category selection and a dish selection are never equal, even on the same id.
        SelectedItem.Category(id).Should().NotBe(SelectedItem.Dish(id));
    }
}
