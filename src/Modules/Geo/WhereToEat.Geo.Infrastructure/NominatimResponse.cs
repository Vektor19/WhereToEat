using System.Text.Json.Serialization;

namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// One result row from the Nominatim <c>/search?format=jsonv2</c> response. Nominatim returns the
/// latitude/longitude as <b>strings</b>, so they are parsed (invariant-culture) by the adapter. Only
/// the OSM lat/lon is read here — there is no Google coordinate to read (invariant #7); the optional
/// Place ID/deep-link the model allows are not provided by Nominatim and stay <c>null</c>.
/// </summary>
public sealed class NominatimResult
{
    /// <summary>The latitude as a string (e.g. "50.4501"); parsed by the adapter.</summary>
    [JsonPropertyName("lat")]
    public string? Lat { get; set; }

    /// <summary>The longitude as a string (e.g. "30.5234"); parsed by the adapter.</summary>
    [JsonPropertyName("lon")]
    public string? Lon { get; set; }

    /// <summary>The human-readable matched place name (logged for diagnostics, not stored).</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
