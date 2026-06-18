using WhereToEat.Catalog.Application.Contracts;
using WhereToEat.Catalog.Domain.Abstractions;
using WhereToEat.Catalog.Domain.Identifiers;

namespace WhereToEat.Catalog.Application.Queries;

/// <summary>
/// Builds the "Глянути на карті" Map payload (§5.7) <b>purely</b> from the restaurant's stored
/// fields: our OSM-sourced coordinates plus the optional Place ID / Maps deep-link. It is live-only
/// and caches nothing (§5.7), never geocodes (the <c>IGeocoder</c> port arrives in Step 8), and
/// never returns a Google rating or Google-sourced coordinate (invariants #6/#7).
///
/// <para>
/// When the restaurant has no stored coordinates, it returns the well-defined "no map payload"
/// shape (<see cref="MapPayloadDto.NoMapData"/>) rather than attempting to geocode. A missing
/// restaurant returns <c>null</c> so the host can answer 404.
/// </para>
/// </summary>
public sealed class GetRestaurantMapPayloadQuery
{
    private readonly ICatalogRepository _repository;

    public GetRestaurantMapPayloadQuery(ICatalogRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>Returns the stored-only Map payload, the "no map data" shape, or <c>null</c> if not found.</summary>
    public async Task<MapPayloadDto?> ExecuteAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var restaurant = await _repository
            .GetRestaurantByIdAsync(RestaurantId.From(restaurantId), cancellationToken)
            .ConfigureAwait(false);

        if (restaurant is null)
        {
            return null;
        }

        // No stored coordinates → no map payload. We never geocode here (Step 8 owns that seam).
        if (restaurant.Coordinates is null)
        {
            return MapPayloadDto.NoMapData(restaurant.Id.Value);
        }

        var coordinates = restaurant.Coordinates;
        return new MapPayloadDto(
            restaurant.Id.Value,
            HasMapData: true,
            coordinates.Point.Latitude,
            coordinates.Point.Longitude,
            coordinates.PlaceId,
            coordinates.MapsDeepLink);
    }
}
