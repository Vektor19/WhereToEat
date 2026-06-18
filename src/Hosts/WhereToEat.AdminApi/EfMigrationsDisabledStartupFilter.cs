using WhereToEat.Admin.Infrastructure;

namespace WhereToEat.AdminApi;

/// <summary>
/// Runs the EF-migrations-disabled guard <b>eagerly during host build</b>: EF maps database-first onto
/// the SQL-script-owned schema and must NEVER own/alter it. As an <see cref="IStartupFilter"/> the
/// guard executes while the request pipeline is constructed (not deferred to the first HTTP request),
/// so any schema drift fails the host start-up — including the moment a test's
/// <c>WebApplicationFactory</c> spins up the server — rather than surfacing mid-request. The guard
/// only reads metadata + <c>INFORMATION_SCHEMA</c>; it never calls <c>EnsureCreated</c>/<c>Migrate</c>.
/// </summary>
internal sealed class EfMigrationsDisabledStartupFilter : IStartupFilter
{
    private readonly IServiceProvider _services;

    public EfMigrationsDisabledStartupFilter(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        // Block on the async guard: IStartupFilter.Configure is synchronous and runs once at build,
        // so failing here aborts host start-up before any request is served.
        EfMigrationsDisabledGuard.EnsureNoSchemaDriftOrThrow(db).GetAwaiter().GetResult();

        return next;
    }
}
