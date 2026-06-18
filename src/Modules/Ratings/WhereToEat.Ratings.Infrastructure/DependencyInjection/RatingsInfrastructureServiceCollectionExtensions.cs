using Microsoft.Extensions.DependencyInjection;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Infrastructure.Persistence;

namespace WhereToEat.Ratings.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Ratings module's Dapper data layer: the shared SQL connection factory (if not
/// already present) and the <see cref="IRatingAggregateRepository"/> domain port (implemented by
/// <see cref="DapperRatingAggregateRepository"/>). Hosts call this from their composition root; kept
/// here so the data layer is independently registrable and integration-testable now.
/// </summary>
public static class RatingsInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ratings persistence services bound to <paramref name="connectionString"/>. The
    /// connection factory is registered as a singleton only if no <see cref="ISqlConnectionFactory"/>
    /// is already present (so a host wiring multiple Dapper modules shares one factory).
    /// </summary>
    public static IServiceCollection AddRatingsPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddScoped<IRatingAggregateRepository, DapperRatingAggregateRepository>();

        // The Step 12 rating-recompute job's worker: rebuilds the materialized aggregate from the raw
        // ratings and upserts it through the repository above (idempotent). Registered here so the
        // worker host gets it by calling AddRatingsPersistence.
        services.AddScoped<IRatingAggregateRecomputer, DapperRatingAggregateRecomputer>();

        return services;
    }
}
