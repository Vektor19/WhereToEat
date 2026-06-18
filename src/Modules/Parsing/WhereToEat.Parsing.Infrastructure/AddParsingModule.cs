using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Application.Compliance;
using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure.Compliance;
using WhereToEat.Parsing.Infrastructure.Examples;
using WhereToEat.Parsing.Infrastructure.Persistence;
using WhereToEat.Parsing.Infrastructure.Strategies;

namespace WhereToEat.Parsing.Infrastructure;

/// <summary>
/// The Parsing module's composition entry point a host/worker calls from its composition root. It
/// wires the full Step 9 pipeline behind its ports:
/// <list type="bullet">
///   <item>the compliance gates that run BEFORE any fetch (invariant #9) — first-party allow-list,
///   robots.txt over a typed <see cref="HttpClient"/>, and the per-host rate-limit on the swappable
///   counter-store seam (in-memory now; Redis behind the cache abstraction in Step 13);</item>
///   <item>the two parser strategies behind one <see cref="IRestaurantParser"/> contract and the
///   <b>example</b> restaurant parsers registered <b>by key</b> through the strategy selector
///   (invariant #4 — a new restaurant self-registers);</item>
///   <item>the normalizer (raw dish → canonical taxonomy via the Contracts dish-resolver seam;
///   unmappable → quarantine, invariant #2) and the Dapper quarantine store;</item>
///   <item>the <see cref="RunParseCommandHandler"/> itself.</item>
/// </list>
/// The catalog/geo seams (<c>ICatalogDishResolver</c>, <c>ICatalogMenuWriter</c>,
/// <c>IRestaurantGeocoder</c>) are supplied by their owning modules' registrations, so a host wires
/// all of them; this module never references Catalog/Geo internals.
/// </summary>
public static class AddParsingModuleExtensions
{
    /// <summary>
    /// Registers the Parsing module's pipeline + strategies + compliance gates, binding options from
    /// <paramref name="configuration"/>'s <c>Parsing</c> sections.
    /// </summary>
    public static IServiceCollection AddParsingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ---- Compliance (invariant #9) -----------------------------------------------------------
        services.AddOptions<FirstPartyAllowListOptions>()
            .Bind(configuration.GetSection(FirstPartyAllowListOptions.SectionName));
        services.AddSingleton<IFirstPartyAllowList, FirstPartyAllowList>();

        services.AddOptions<HostRateLimitOptions>()
            .Bind(configuration.GetSection(HostRateLimitOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        // The rate-limit counter store is process-wide (one last-claim map per process); Step 13 swaps
        // in the Redis-backed counter behind the cache abstraction without touching the gate.
        services.AddSingleton<IRateLimitCounterStore, InMemoryRateLimitCounterStore>();
        services.AddSingleton<IHostRateLimiter, HostRateLimiter>();

        services.AddHttpClient<IRobotsTxtGate, RobotsTxtGate>();

        // ---- Strategies + the keyed example restaurant parsers (invariant #4) --------------------
        services.AddSingleton<IWebDriverFactory, ChromeWebDriverFactory>();
        services.AddSingleton<SeleniumParserStrategy>();
        services.AddSingleton<AngleSharpParserStrategy>();

        // Each example registers as an IRestaurantParser under its own key; the selector builds the
        // key → parser map from every registered IRestaurantParser, so adding a restaurant is purely
        // "add an example type + fixture + test", with no selector/pipeline change.
        services.AddSingleton<IRestaurantParser, BorschCafeSeleniumParser>();
        services.AddSingleton<IRestaurantParser, PizzaHouseAngleSharpParser>();
        services.AddSingleton<IParserStrategySelector, ParserStrategySelector>();

        // ---- Normalize + quarantine (invariant #2) -----------------------------------------------
        services.AddScoped<INormalizer, Normalizer>();
        services.AddScoped<IParseQuarantineStore, DapperParseQuarantineStore>();

        // ---- The pipeline handler ----------------------------------------------------------------
        services.AddScoped<RunParseCommandHandler>();

        return services;
    }
}
