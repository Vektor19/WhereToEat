using WhereToEat.Catalog.Application.Contracts;
using WhereToEat.Catalog.Domain.Abstractions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;

namespace WhereToEat.Catalog.Application.Queries;

/// <summary>
/// Loads a restaurant's details. The result <b>always</b> includes the website/social
/// <see cref="ContactLink"/>s (§5.8): they are shown for free even for non-Verified venues and are
/// never monetized (invariant #10). Returns <c>null</c> when no such restaurant exists.
/// </summary>
public sealed class GetRestaurantDetailsQuery
{
    private readonly ICatalogRepository _repository;

    public GetRestaurantDetailsQuery(ICatalogRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>Returns the details DTO (contact links always populated), or <c>null</c> if not found.</summary>
    public async Task<RestaurantDetailsDto?> ExecuteAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var restaurant = await _repository
            .GetRestaurantByIdAsync(RestaurantId.From(restaurantId), cancellationToken)
            .ConfigureAwait(false);

        if (restaurant is null)
        {
            return null;
        }

        // Contact links are surfaced unconditionally — §5.8 / invariant #10. There is no
        // Verified/paid gate here by design.
        var contactLinks = restaurant.ContactLinks
            .Select(link => new ContactLinkDto(link.Kind.ToString(), link.Url, link.Label))
            .ToList();

        var menuItems = restaurant.MenuItems
            .Select(item => new MenuItemDto(
                item.Id.Value,
                item.DishId.Value,
                item.Price.Amount,
                item.Price.Currency,
                item.Weight))
            .ToList();

        return new RestaurantDetailsDto(
            restaurant.Id.Value,
            restaurant.Name,
            restaurant.Address.Line,
            restaurant.Address.City,
            contactLinks,
            menuItems);
    }
}
