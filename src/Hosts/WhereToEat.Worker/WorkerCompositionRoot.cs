using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.BuildingBlocks.Messaging;
using WhereToEat.Catalog.Infrastructure.DependencyInjection;
using WhereToEat.Geo.Infrastructure;
using WhereToEat.Parsing.Infrastructure;
using WhereToEat.Ratings.Infrastructure.DependencyInjection;
using WhereToEat.Recommendation.Infrastructure.DependencyInjection;
using WhereToEat.Worker.Scheduling;

namespace WhereToEat.Worker;

/// <summary>
/// The worker host's composition root (kept out of <c>Program.cs</c> so the integration tests can wire
/// the same services without the generic-host bootstrapping). It registers the module Application/
/// Infrastructure the four jobs delegate to, the host-level parse-source seam, and — the heart of this
/// step — Quartz.NET configured with a <b>clustered, DB-backed (SQL Server AdoJobStore) job store</b>.
/// <para>
/// The clustered persistent store is what makes a scheduled trigger fire on <b>exactly one</b> replica
/// when several workers point at the same DB: Quartz uses the QRTZ_* cluster tables (migration 0013) to
/// elect a single node per fire. <c>SchedulerName</c> is shared across replicas (they form one cluster);
/// the <c>InstanceId</c> is auto-generated per replica so each is a distinct cluster member.
/// </para>
/// </summary>
public static class WorkerCompositionRoot
{
    /// <summary>
    /// Registers the worker's modules, the parse-source seam, the job-options, and the clustered Quartz
    /// scheduler (jobs + cron triggers via <see cref="JobSchedule"/>) against
    /// <paramref name="connectionString"/>. The hosted Quartz service itself is added by the caller via
    /// <c>AddQuartzHostedService</c> (so a test can register the schedule without starting it).
    /// </summary>
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        AddJobDependencies(services, configuration, connectionString);
        AddClusteredQuartz(services, connectionString);

        return services;
    }

    /// <summary>
    /// Wires the module services the four jobs depend on, plus the host-level parse-source provider and
    /// the median-job options. Split out so a test can register only the job dependencies (without the
    /// scheduler) when it drives a job directly.
    /// </summary>
    public static IServiceCollection AddJobDependencies(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Step 13: the Redis cache (parser per-host rate-limit counters + read-through caches) behind
        // ICacheService, and the MassTransit bus carrying the Contracts integration events (the worker
        // hosts the geocode-on-AddressChanged + invalidate-cache consumers). Both degrade gracefully:
        // a host without Redis configured gets the NullCacheService; the bus defaults to in-process.
        services.AddRedisCache(configuration);
        services.AddMessaging(configuration);

        // (1) Weekly parse — the Step 9 pipeline (compliance + admin-protected persist) + its source list.
        // Step 13 closes the Step 12 deferral: AddCatalogPersistence now registers the production
        // ICatalogMenuWriter/ICatalogDishResolver adapters and AddGeoModule registers the production
        // IRestaurantGeocoder adapter, so RunParseCommandHandler's full dependency graph resolves and
        // WeeklyParseJob no longer throws at its first Quartz fire (proven by the DI-resolution test).
        services.AddParsingModule(configuration);
        services.AddOptions<ParseSourceOptions>().Bind(configuration.GetSection(ParseSourceOptions.SectionName));
        services.AddSingleton<IParseSourceProvider, ConfigurationParseSourceProvider>();

        // (2) Geocode refresh — the Step 8 geocoder + AddressChanged seam, over the catalog coordinate
        // writer (which also exposes the "needs geocode" queue read). AddGeoModule also registers the
        // production IRestaurantGeocoder adapter the parse job + the bus geocode consumer resolve to.
        services.AddGeoModule(configuration);
        services.AddCatalogPersistence(connectionString);

        // (3) Nightly price-median — the DB-only writer over the Step 7 median table (no Redis).
        services.AddRecommendationModule(connectionString);
        services.AddOptions<Jobs.PriceMedianJobOptions>().Bind(configuration.GetSection(Jobs.PriceMedianJobOptions.SectionName));

        // (4) Rating recompute — the Ratings recomputer over the raw ratings → materialized aggregate.
        services.AddRatingsPersistence(connectionString);

        return services;
    }

    private static void AddClusteredQuartz(IServiceCollection services, string connectionString)
    {
        services.AddQuartz(quartz =>
        {
            // One shared scheduler name => all replicas form a single cluster over the same store.
            quartz.SchedulerName = "WhereToEatWorkerScheduler";

            quartz.UsePersistentStore(store =>
            {
                // CLUSTERED: the DB store + cluster election is what guarantees a trigger fires on
                // exactly one replica (the QRTZ_* tables from migration 0013 back this).
                store.UseClustering(cluster =>
                {
                    // Check-in cadence: a member missing two intervals is considered failed and its
                    // in-flight recovery-requesting jobs are re-fired elsewhere. Kept short so the test
                    // and a real failover both react quickly.
                    cluster.CheckinInterval = TimeSpan.FromSeconds(10);
                    cluster.CheckinMisfireThreshold = TimeSpan.FromSeconds(20);
                });

                store.UseSqlServer(sql =>
                {
                    sql.ConnectionString = connectionString;
                    // The migration scripts create the QRTZ_ tables with the default prefix; the default
                    // table prefix and schema (dbo) match 0013, so no override is needed.
                });

                // Our jobs carry no JobDataMap payload, so the default serializer is sufficient; the
                // cluster election uses the QRTZ_LOCKS / fired-trigger rows, not job-data serialization.
                store.UseProperties = true;
            });

            // Misfires that pile up are batched conservatively so a recovering cluster does not stampede.
            quartz.MisfireThreshold = TimeSpan.FromSeconds(60);

            JobSchedule.Configure(quartz);
        });
    }
}
