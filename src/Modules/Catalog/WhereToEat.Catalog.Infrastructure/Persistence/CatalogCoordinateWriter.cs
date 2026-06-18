using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Domain.Abstractions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.Catalog.Infrastructure.Persistence.Sql;
using WhereToEat.Contracts.Geo;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Infrastructure.Persistence;

/// <summary>
/// The Catalog-side adapter for the Geo module's <see cref="ICatalogCoordinateWriter"/> seam (Step 8).
/// It reads a restaurant's address-to-geocode and writes the geocoded coordinates back, going through
/// the catalog's own <see cref="ICatalogRepository"/> aggregate load/save — so the Geo module never
/// references Catalog internals (it depends only on the <c>Contracts</c> port; the coupling lives here,
/// inside the Catalog module, keeping the Step 2 module-isolation rule green).
///
/// The write path rebuilds the OSM-only <see cref="Coordinates"/> value object from the incoming
/// <see cref="GeoCoordinatesDto"/>; because <see cref="Coordinates"/> has no "from Google coords"
/// factory, this cannot store raw Google lat/lng (invariant #7). An out-of-range coordinate is
/// rejected (returns <c>false</c>) rather than persisted.
/// </summary>
public sealed class CatalogCoordinateWriter : ICatalogCoordinateWriter
{
    private readonly ICatalogRepository _repository;
    private readonly ISqlConnectionFactory _connectionFactory;

    public CatalogCoordinateWriter(ICatalogRepository repository, ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _repository = repository;
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<string?> GetGeocodingAddressAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var restaurant = await _repository
            .GetRestaurantByIdAsync(RestaurantId.From(restaurantId), cancellationToken)
            .ConfigureAwait(false);

        // The Address value object's ToString() is exactly the single line (+ city) the geocoder takes.
        return restaurant?.Address.ToString();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetRestaurantIdsNeedingGeocodeAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount < 1)
        {
            return Array.Empty<Guid>();
        }

        // A lightweight id-only read (no aggregate hydration) for the geocode-refresh job's queue; it
        // stays inside the Catalog module (Geo only sees the Contracts port), so module isolation holds.
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = await connection.QueryAsync<Guid>(
                new CommandDefinition(
                    CatalogSql.SelectRestaurantIdsNeedingGeocode,
                    new { MaxCount = maxCount },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return ids.ToList();
    }

    /// <inheritdoc />
    public async Task<bool> SetCoordinatesAsync(
        Guid restaurantId,
        GeoCoordinatesDto coordinates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        var restaurant = await _repository
            .GetRestaurantByIdAsync(RestaurantId.From(restaurantId), cancellationToken)
            .ConfigureAwait(false);

        if (restaurant is null)
        {
            return false;
        }

        // Validate the OSM-sourced point through the SharedKernel guard; reject (don't persist) an
        // out-of-range coordinate rather than throwing into the geocode flow.
        var pointResult = GeoPoint.Create(coordinates.Latitude, coordinates.Longitude);
        if (pointResult.IsFailure)
        {
            return false;
        }

        var coordinatesResult = Coordinates.FromOsmGeoPoint(
            pointResult.Value,
            coordinates.PlaceId,
            coordinates.MapsDeepLink);
        if (coordinatesResult.IsFailure)
        {
            return false;
        }

        restaurant.SetCoordinates(coordinatesResult.Value);
        await _repository.UpdateRestaurantAsync(restaurant, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
