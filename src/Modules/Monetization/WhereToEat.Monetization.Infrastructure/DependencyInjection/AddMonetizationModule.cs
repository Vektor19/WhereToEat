using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Monetization.Application.Ads;
using WhereToEat.Monetization.Application.Payments;
using WhereToEat.Monetization.Application.Verified;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Infrastructure.Payments;
using WhereToEat.Monetization.Infrastructure.Persistence;

namespace WhereToEat.Monetization.Infrastructure.DependencyInjection;

/// <summary>
/// The Monetization module's composition entry point the admin host calls from its composition root
/// (Step 14, future-facing — no real billing). It wires the Dapper repositories for the domain ports,
/// the grant/revoke-Verified + create-labeled-ad use-case handlers, and the dev no-op
/// <see cref="IPaymentGateway"/> (the SOLE payment touch-point — a future real provider plugs in here).
/// Granting Verified only flips the Step 5 real-photo gate + sets the tier; nothing here feeds organic
/// ranking (invariant #10) or touches the always-free contact links (§5.8). Dapper only — no EF.
/// </summary>
public static class AddMonetizationModuleExtensions
{
    /// <summary>
    /// Registers the Monetization data layer + use-cases + the no-op payment gateway bound to
    /// <paramref name="connectionString"/>. The connection factory is registered only if no
    /// <see cref="ISqlConnectionFactory"/> is already present (so a host wiring multiple Dapper modules
    /// shares one factory). The no-op gateway is registered with <c>TryAdd</c> so a host that later
    /// supplies a real <see cref="IPaymentGateway"/> overrides it without code change here.
    /// </summary>
    public static IServiceCollection AddMonetizationModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        services.TryAddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        services.AddScoped<IVerifiedStatusRepository, DapperVerifiedStatusRepository>();
        services.AddScoped<IAdPlacementRepository, DapperAdPlacementRepository>();

        // The dev no-op gateway is the SOLE payment implementation (real billing is a non-goal). TryAdd
        // so a future host can register a real provider in front of it without touching this module.
        services.TryAddSingleton<IPaymentGateway, NoOpPaymentGateway>();

        services.AddScoped<GrantVerifiedCommandHandler>();
        services.AddScoped<RevokeVerifiedCommandHandler>();
        services.AddScoped<CreateAdPlacementCommandHandler>();

        return services;
    }
}
