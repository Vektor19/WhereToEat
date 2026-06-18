namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// Configuration for the Nominatim geocoder adapter, bound from the <c>Nominatim</c> appsettings
/// section. The defaults encode Nominatim's usage policy: a public base URL, a <b>descriptive,
/// contactable User-Agent</b> (Nominatim rejects generic/empty agents), and a polite
/// <b>minimum request interval</b> (its policy is ≤ 1 request/second for the public server).
/// </summary>
public sealed class NominatimOptions
{
    /// <summary>The appsettings section these options bind from.</summary>
    public const string SectionName = "Nominatim";

    /// <summary>The Nominatim base URL (the public server by default). Must be absolute.</summary>
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>
    /// The descriptive, contactable User-Agent sent on every request (Nominatim requires identifying
    /// the application + a contact). Sending a blank/generic agent gets the caller blocked.
    /// </summary>
    public string UserAgent { get; set; } = "WhereToEat/1.0 (ДеПоїсти; contact: ops@wheretoeat.example)";

    /// <summary>
    /// The attribution string we carry alongside stored OSM coordinates (ODbL requires attribution).
    /// </summary>
    public string Attribution { get; set; } = "© OpenStreetMap contributors (ODbL)";

    /// <summary>
    /// The minimum interval between outbound requests (the rate-limit). Nominatim's public policy is
    /// at most one request per second, so the default is 1000 ms. Set to zero only against a private
    /// instance.
    /// </summary>
    public int MinRequestIntervalMs { get; set; } = 1000;

    /// <summary>Per-request HTTP timeout in milliseconds (the typed client + Polly timeout).</summary>
    public int RequestTimeoutMs { get; set; } = 10_000;

    /// <summary>How many times Polly retries a transient HTTP failure before giving up.</summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>The base back-off (ms) between retries; grows with the attempt number.</summary>
    public int RetryBaseDelayMs { get; set; } = 500;
}
