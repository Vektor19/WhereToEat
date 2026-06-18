using WhereToEat.Contracts.Geo;
using WhereToEat.Geo.Application;

namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// The production Geo-side adapter for the Parsing module's <see cref="IRestaurantGeocoder"/> seam
/// (Step 9 / Step 13) and the bus's geocode-on-address-changed consumer (Step 13). It bridges the
/// Contracts port to the Step 8 <see cref="GeocodeRestaurantCommandHandler"/> (OSM/Nominatim → the
/// catalog coordinate-writer seam), so callers depend only on <c>Contracts</c> and never reference the
/// Geo module's <c>IGeocoder</c>/command internals — the coupling stays inside Geo (Step 2
/// module-isolation rule (d) green).
/// <para>
/// Best-effort: a failed geocode (no match, transport error, unknown restaurant) returns <c>false</c>
/// rather than throwing, so a parse run / address-change consume continues without faulting.
/// </para>
/// </summary>
public sealed class RestaurantGeocoderAdapter : IRestaurantGeocoder
{
    private readonly GeocodeRestaurantCommandHandler _handler;

    public RestaurantGeocoderAdapter(GeocodeRestaurantCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    /// <inheritdoc />
    public async Task<bool> GeocodeAndStoreAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var result = await _handler
            .HandleAsync(new GeocodeRestaurantCommand(restaurantId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess;
    }
}
