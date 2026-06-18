using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.Admin.Application.Addresses;
using WhereToEat.Admin.Application.MenuItems;
using WhereToEat.Admin.Application.Photos;
using WhereToEat.Admin.Application.Protection;
using WhereToEat.Admin.Infrastructure.Persistence;

namespace WhereToEat.Admin.Infrastructure;

/// <summary>
/// The Admin module's composition entry point the admin host calls from its composition root. It
/// wires the EF Core context (database-first, NO migrations), the EF-backed
/// <see cref="IAdminCatalogStore"/>, and the admin CRUD use-case handlers. EF lives ONLY here (the
/// Step 2 EF-containment fitness rule). The address-changed dispatcher (the Step 8 re-geocode seam) is
/// host-supplied — the admin host registers an adapter that invokes the Geo handler — so this module
/// never references Geo internals (module-isolation rule d).
/// </summary>
public static class AddAdminModuleExtensions
{
    /// <summary>
    /// Registers the Admin module's EF context + store + handlers against
    /// <paramref name="connectionString"/>. The context is configured so it NEVER owns/alters the
    /// schema (the SQL scripts are the schema-of-record); the <see cref="EfMigrationsDisabledGuard"/>
    /// is run by the host at startup to fail fast on any drift.
    /// </summary>
    public static IServiceCollection AddAdminModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        services.AddDbContext<AdminDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // Pin the migrations assembly to this assembly (where there is NO Migrations folder),
                // so EF can never pick up a stray migration from elsewhere. The guard asserts the
                // count is zero at startup.
                sql.MigrationsAssembly(typeof(AdminDbContext).Assembly.GetName().Name);
            });
        });

        services.AddScoped<IAdminCatalogStore, EfAdminCatalogStore>();

        // Admin CRUD use-case handlers (invariant #3 protection surface + invariant #8 photo gate).
        services.AddScoped<EditMenuItemCommandHandler>();
        services.AddScoped<SetMenuItemDoNotParseCommandHandler>();
        services.AddScoped<SetRestaurantDoNotUpdateCommandHandler>();
        services.AddScoped<EditAddressCommandHandler>();
        services.AddScoped<ManageRealPhotoCommandHandler>();

        return services;
    }
}
