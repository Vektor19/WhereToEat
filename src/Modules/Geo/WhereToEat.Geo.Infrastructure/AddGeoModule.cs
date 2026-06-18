using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.Extensions.Http;
using WhereToEat.Contracts.Geo;
using WhereToEat.Geo.Application;

namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// The Geo module's composition entry point a host/worker calls from its composition root: it binds
/// <see cref="NominatimOptions"/> from the <c>Nominatim</c> appsettings section, registers a
/// <b>typed <see cref="HttpClient"/></b> for <see cref="NominatimGeocoder"/> with a descriptive
/// User-Agent + base URL + timeout, attaches a <b>Polly</b> resilience policy (retry-with-backoff on
/// transient HTTP failures), and wires the geocode command + AddressChanged handler.
/// <para>
/// The polite minimum-request-interval rate-limit lives in a <b>singleton</b>
/// <see cref="NominatimRateLimiter"/> (process-wide state — one outbound interval per process, shared
/// across every scoped handler) fronted by a <see cref="RateLimitingHandler"/>. The pipeline is built
/// so <b>Polly is the outer handler and the rate-limit handler is the inner</b> one: each Polly retry
/// re-invokes the inner handler and therefore re-enters the rate limiter, so every attempt (not just
/// the first) is throttled — a fast-failing upstream cannot breach the interval.
/// </para>
/// </summary>
public static class AddGeoModuleExtensions
{
    /// <summary>
    /// Registers the Geo module (Nominatim geocoder + typed client + Polly + the geocode/AddressChanged
    /// handlers), binding options from <paramref name="configuration"/>'s <c>Nominatim</c> section. The
    /// catalog coordinate-writer seam (<c>ICatalogCoordinateWriter</c>) is supplied by the Catalog
    /// module's registration, so a host wires both.
    /// </summary>
    public static IServiceCollection AddGeoModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<NominatimOptions>()
            .Bind(configuration.GetSection(NominatimOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Nominatim.BaseUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserAgent), "Nominatim.UserAgent is required (usage policy).")
            .ValidateOnStart();

        // The rate-limit state is process-wide: one singleton limiter (one semaphore + last-request
        // timestamp) for the whole process, so N concurrent geocodes share a single outbound interval.
        services.AddSingleton<NominatimRateLimiter>();
        services.AddTransient<RateLimitingHandler>();

        // Pipeline ordering is load-bearing: AddPolicyHandler is registered FIRST so it is the OUTER
        // handler, and the RateLimitingHandler is registered AFTER it so it is the INNER one. A Polly
        // retry re-invokes the inner pipeline and thus re-enters the rate limiter — every retry is
        // re-throttled, not just the first attempt.
        services.AddHttpClient<IGeocoder, NominatimGeocoder>(ConfigureClient)
            .AddPolicyHandler(BuildRetryPolicy)
            .AddHttpMessageHandler<RateLimitingHandler>();

        // The use-case handlers (no extra deps beyond the geocoder + the Contracts coordinate seam).
        services.AddScoped<GeocodeRestaurantCommandHandler>();
        services.AddScoped<GeocodeOnAddressChangedHandler>();

        // The production IRestaurantGeocoder seam (Step 13): bridges the Contracts port to the Step 8
        // command handler so the Parsing WeeklyParseJob and the bus's geocode consumer resolve a real
        // adapter (no more test-double-only / first-fire throw). TryAdd so a test double still wins.
        services.TryAddScoped<IRestaurantGeocoder, RestaurantGeocoderAdapter>();

        return services;
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NominatimOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs);

        // Nominatim's usage policy requires a descriptive, contactable User-Agent on every request.
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(IServiceProvider provider, HttpRequestMessage _)
    {
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NominatimOptions>>().Value;

        // Retry transient HTTP errors (5xx, 408, network) with a linear back-off. We do NOT retry on
        // a 429 aggressively — the in-geocoder interval is the primary politeness control.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                Math.Max(0, options.RetryCount),
                attempt => TimeSpan.FromMilliseconds(options.RetryBaseDelayMs * attempt));
    }
}
