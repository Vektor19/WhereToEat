using System.Globalization;
using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Infrastructure.Persistence.Mappings;
using WhereToEat.Catalog.Infrastructure.Persistence.Sql;
using WhereToEat.SharedKernel.Geo;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Infrastructure.Persistence;

/// <summary>
/// The Dapper implementation of the geography radius pre-filter. It runs the spatial-index-assisted
/// SQL (<see cref="CatalogSql.SelectRestaurantsWithinRadius"/>) to fetch only the candidates within
/// the radius, then computes the exact distance and ordering in code with
/// <see cref="Haversine"/> — the SQL narrows, Haversine decides (invariant #7). The SQL distance is
/// only the coarse gate; final ordering never relies on the DB's metric.
/// </summary>
internal sealed class DapperCatalogSpatialReader : ICatalogSpatialReader
{
    private const double MetersPerKm = 1000d;

    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperCatalogSpatialReader(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NearbyRestaurant>> FindWithinRadiusAsync(
        GeoPoint origin,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (radiusKm < 0 || !double.IsFinite(radiusKm))
        {
            throw new ArgumentOutOfRangeException(nameof(radiusKm), radiusKm, "Radius must be a non-negative, finite number of kilometres.");
        }

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // WKT is longitude-first; invariant culture keeps the decimal separator a dot.
        var wkt = string.Format(
            CultureInfo.InvariantCulture,
            "POINT({0} {1})",
            origin.Longitude,
            origin.Latitude);

        var rows = await connection.QueryAsync<RestaurantRow>(
            new CommandDefinition(
                CatalogSql.SelectRestaurantsWithinRadius,
                new { Wkt = wkt, RadiusMeters = radiusKm * MetersPerKm },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var nearby = new List<NearbyRestaurant>();
        foreach (var row in rows)
        {
            // The pre-filter SQL only returns rows with a non-null Location, so lat/lng are present.
            var pointResult = GeoPoint.Create(row.Latitude!.Value, row.Longitude!.Value);
            if (pointResult.IsFailure)
            {
                continue;
            }

            var point = pointResult.Value;
            var distanceKm = Haversine.DistanceKm(origin, point);
            nearby.Add(new NearbyRestaurant(RestaurantId.From(row.Id), row.Name, point, distanceKm));
        }

        // Exact ordering is decided here, in code, by the Haversine distance — not by the DB metric.
        return nearby
            .OrderBy(r => r.DistanceKm)
            .ToList();
    }
}
