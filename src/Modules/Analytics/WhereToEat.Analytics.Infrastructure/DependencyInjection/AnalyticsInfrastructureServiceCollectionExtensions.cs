using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain.Abstractions;
using WhereToEat.Analytics.Infrastructure.Anonymization;
using WhereToEat.Analytics.Infrastructure.Persistence;
using WhereToEat.BuildingBlocks.Persistence;

namespace WhereToEat.Analytics.Infrastructure.DependencyInjection;

/// <summary>
/// The Analytics module's composition entry point (the modular-monolith convention). It wires the full
/// ingest path (Step 11): the shared SQL connection factory, the append-optimized Dapper writer
/// (<see cref="IAnalyticsEventStore"/>), the rotating-salt anonymization pipeline
/// (<see cref="ISaltProvider"/> + <see cref="IAnonymizer"/>) that runs <b>before</b> persistence, the
/// <see cref="IngestEventCommandHandler"/>, and the <see cref="IAnalyticsRollupReader"/> aggregation
/// seam (aggregates only — invariant #11).
/// </summary>
public static class AnalyticsInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Environment names where a blank salt master secret is tolerated (the anonymization-at-ingest
    /// HMAC still keys on the rotating salt, but the salt no longer mixes a secret). Anywhere else a
    /// blank secret would make the actor-id hash brute-forceable from the public salt alone, so it
    /// fails fast — mirrors the Dev/Testing-only relaxation of the OIDC authority guard (Step 6/10).
    /// </summary>
    private const string DevelopmentEnvironment = "Development";
    private const string TestingEnvironment = "Testing";

    /// <summary>
    /// Adds the analytics append path only (Step 5 surface). Retained for the integration suite that
    /// exercises the writer in isolation; the full module is wired by <see cref="AddAnalyticsModule"/>.
    /// </summary>
    public static IServiceCollection AddAnalyticsPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        services.TryAddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddScoped<IAnalyticsEventStore, AppendOnlyAnalyticsWriter>();

        return services;
    }

    /// <summary>
    /// Wires the full Analytics ingest module against <paramref name="connectionString"/>, with optional
    /// <paramref name="configureOptions"/> for the geohash precision / salt-rotation window / master
    /// secret (privacy-safe defaults applied when omitted).
    /// <para>
    /// <paramref name="environmentName"/> gates the privacy fail-fast: outside Development/Testing a
    /// null/whitespace <see cref="AnonymizerOptions.MasterSecret"/> is rejected at startup, because the
    /// actor-id HMAC would then key on the public rotating-salt window index alone — brute-forceable,
    /// defeating anonymization (invariant #11). The host passes
    /// <c>IWebHostEnvironment.EnvironmentName</c>.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAnalyticsModule(
        this IServiceCollection services,
        string connectionString,
        string environmentName,
        Action<AnonymizerOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        // The connection factory may already be registered by another module's AddXxxModule; TryAdd
        // keeps a single factory for the host.
        services.TryAddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        var options = new AnonymizerOptions();
        configureOptions?.Invoke(options);

        // Fail fast: outside Dev/Testing a blank master secret would leave the actor-id HMAC keyed on
        // the public salt-window index alone (brute-forceable). A missing prod secret is a
        // misconfiguration, not a reason to ship weakened anonymization — surface it at startup.
        var isDevOrTest =
            string.Equals(environmentName, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, TestingEnvironment, StringComparison.OrdinalIgnoreCase);

        if (!isDevOrTest && string.IsNullOrWhiteSpace(options.MasterSecret))
        {
            throw new InvalidOperationException(
                $"'Analytics:SaltMasterSecret' must be configured in the '{environmentName}' environment " +
                "— the anonymization-at-ingest HMAC keys on the master secret plus the rotating salt, and " +
                "must not run with a brute-forceable salt-only key (invariant #11). Set a non-empty secret.");
        }

        services.AddSingleton(options);

        // Anonymization-at-ingest: the salt rotates on a schedule; the anonymizer applies retain/hash/
        // drop BEFORE anything is persisted.
        services.AddSingleton<ISaltProvider>(sp => new RotatingSaltProvider(sp.GetRequiredService<AnonymizerOptions>()));
        services.AddSingleton<IAnonymizer>(sp => new Anonymizer(
            sp.GetRequiredService<ISaltProvider>(),
            sp.GetRequiredService<AnonymizerOptions>()));

        // The append-optimized writer + the ingest handler + the aggregates-only rollup seam.
        services.AddScoped<IAnalyticsEventStore, AppendOnlyAnalyticsWriter>();
        services.AddScoped<IngestEventCommandHandler>();
        services.AddScoped<IAnalyticsRollupReader, AnalyticsRollupReader>();

        return services;
    }
}
