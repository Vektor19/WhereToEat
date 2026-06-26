using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Application.Abstractions;
using WhereToEat.Ratings.Application.Submit;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Infrastructure.Messaging;
using WhereToEat.Ratings.Infrastructure.Persistence;

namespace WhereToEat.Ratings.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Ratings <b>write path</b> the submit-rating use-case needs: the
/// <see cref="IRatingRepository"/> write port (Dapper adapter over <c>ratings.Rating</c>), the
/// <see cref="IRatingGivenPublisher"/> seam (MassTransit adapter), the <see cref="TimeProvider"/> the
/// handler stamps <c>GivenAt</c> from, and the <see cref="SubmitRatingCommandHandler"/> itself.
/// <para>
/// This is deliberately <b>separate from</b> <c>AddRatingsPersistence</c> (the Worker-host path, which
/// only needs the aggregate repository + recomputer for the recompute job). The <b>Public host</b> calls
/// this (Step 22) so it gets the rating-write path; the Worker keeps calling the unchanged
/// <c>AddRatingsPersistence</c> and gains no write dependency. The shared connection factory is
/// registered with <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}"/> so a
/// host wiring several Dapper modules shares one factory, and the publish seam requires the host to have
/// already wired MassTransit (<c>AddMessaging</c>), which the Public host does.
/// </para>
/// </summary>
public static class RatingsApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the rating-submit write path bound to <paramref name="connectionString"/>: the write port +
    /// its Dapper adapter, the MassTransit-backed <see cref="RatingGiven"/> publisher, the submit handler,
    /// and a shared <see cref="TimeProvider"/>. The connection factory is added only if no
    /// <see cref="ISqlConnectionFactory"/> is already present.
    /// </summary>
    public static IServiceCollection AddRatingsApplication(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        // Share one connection factory across the host's Dapper modules (TryAdd: a module already
        // wiring it wins, so the Public host's catalog/recommendation/ratings stacks reuse one factory).
        services.TryAddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        // The deterministic time source the handler stamps GivenAt from (TryAdd so a host/test that
        // already registered a fake TimeProvider keeps it).
        services.TryAddSingleton(TimeProvider.System);

        // The write side: the Dapper IRatingRepository adapter and the MassTransit RatingGiven publisher.
        services.AddScoped<IRatingRepository, DapperRatingRepository>();
        services.AddScoped<IRatingGivenPublisher, MassTransitRatingGivenPublisher>();

        // The submit-rating use-case (revise-or-create → persist → publish RatingGiven, invariant #6).
        services.AddScoped<SubmitRatingCommandHandler>();

        return services;
    }
}
