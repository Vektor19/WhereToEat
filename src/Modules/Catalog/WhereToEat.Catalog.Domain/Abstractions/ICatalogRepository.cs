using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.Catalog.Domain.Taxonomy;

namespace WhereToEat.Catalog.Domain.Abstractions;

/// <summary>
/// The catalog <b>write-side</b> persistence port, expressed purely in domain terms: it loads and
/// saves whole aggregates (<see cref="Restaurant"/>, <see cref="Category"/>, <see cref="Dish"/>) by
/// their strongly-typed ids and returns domain objects — <b>no SQL, no connection, no row/DTO
/// shapes</b> leak through here. It is the load/save path for the parser/admin command side; the
/// list/prefix-search reads that back the public read + deterministic-search use-cases live on the
/// separate query-side <c>ICatalogReadPort</c> (Catalog.Application) so the two concerns do not
/// share one fat port. The Dapper adapter implements both ports.
/// </summary>
public interface ICatalogRepository
{
    /// <summary>Loads a restaurant aggregate (with its menu items and contact links), or <c>null</c>.</summary>
    Task<Restaurant?> GetRestaurantByIdAsync(RestaurantId id, CancellationToken cancellationToken = default);

    /// <summary>Loads a category by id, or <c>null</c> when none exists.</summary>
    Task<Category?> GetCategoryByIdAsync(CategoryId id, CancellationToken cancellationToken = default);

    /// <summary>Loads a dish by id, or <c>null</c> when none exists.</summary>
    Task<Dish?> GetDishByIdAsync(DishId id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new restaurant aggregate.</summary>
    Task AddRestaurantAsync(Restaurant restaurant, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing restaurant aggregate (menu, coordinates, address…).</summary>
    Task UpdateRestaurantAsync(Restaurant restaurant, CancellationToken cancellationToken = default);

    /// <summary>Adds a new category.</summary>
    Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Adds a new dish under its category.</summary>
    Task AddDishAsync(Dish dish, CancellationToken cancellationToken = default);
}
