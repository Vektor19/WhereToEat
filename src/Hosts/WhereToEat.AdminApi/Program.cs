using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.Admin.Infrastructure;
using WhereToEat.AdminApi;
using WhereToEat.AdminApi.Endpoints;
using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.BuildingBlocks.Messaging;
using WhereToEat.BuildingBlocks.Observability;
using WhereToEat.Catalog.Infrastructure.DependencyInjection;
using WhereToEat.Geo.Infrastructure;
using WhereToEat.Monetization.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// --- Observability first so every later registration logs through Serilog (Step 13) ----------
builder.Services.AddObservability(builder.Configuration, "WhereToEat.AdminApi");

// --- Configuration ---------------------------------------------------------------------------
// The admin host is a SEPARATE process from the public host (its own port/config — security
// isolation per the design). It points at the same database but exposes only admin-authorized CRUD.
var connectionString = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Catalog' — the admin host needs the catalog DB.");

// --- Modules ---------------------------------------------------------------------------------
// The Admin module: EF Core (the ONLY place EF is allowed) mapping database-first onto the existing
// SQL-script-owned schema; EF migrations are disabled (the guard below fails fast on any drift).
builder.Services.AddAdminModule(connectionString);

// The Geo module supplies the Step 8 re-geocode handler the address-edit flow dispatches to, and the
// Catalog module supplies the coordinate-writer seam Geo writes through (the admin host composes both).
builder.Services.AddGeoModule(builder.Configuration);
builder.Services.AddCatalogPersistence(connectionString);

// The Monetization module (Step 14, future-facing — no real billing): grant/revoke Verified + create
// labeled ad placements behind the no-op IPaymentGateway seam. Granting Verified only flips the Step 5
// real-photo gate + sets the tier; nothing here feeds organic ranking (invariant #10) or the
// always-free contact links (§5.8). The admin endpoints below manage it under the admin-only policy.
builder.Services.AddMonetizationModule(connectionString);

// The in-process AddressChanged → re-geocode dispatcher (the Step 8 seam). Step 13 swaps this for a
// MassTransit publish without touching the admin use-case.
builder.Services.AddScoped<IAddressChangedDispatcher, InProcessAddressChangedDispatcher>();

// --- OIDC resource-server auth (validate the SAME IdP tokens as the public host) -------------
// The admin host validates identical tokens but its endpoints require the ADMIN-ONLY policy, so a
// non-admin authenticated user is rejected with 403 (admin → 200, anonymous → 401). The Dev/Testing
// test-signing-key seam is the same one the public host uses; it is unreachable outside Dev/Testing.
builder.Services.AddOidcResourceServerAuth(builder.Configuration, builder.Environment.EnvironmentName);

// --- Step 13 cross-cutting infrastructure ----------------------------------------------------
// Redis cache (ICacheService; NullCacheService when disabled) + MassTransit (Contracts events,
// in-process now / RabbitMQ by config). The admin host already wires Geo (re-geocode handler) and
// Catalog persistence above, so the bus's geocode + cache-invalidate consumers resolve here too.
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);

// --- EF migrations-disabled startup guard (fail fast on schema drift) ------------------------
// EF must NEVER own/alter the schema — the SQL scripts are the schema-of-record. Registered as an
// IStartupFilter so the guard runs EAGERLY while the host's request pipeline is built (not deferred
// to the first HTTP request): any drift aborts start-up, including when a test's WebApplicationFactory
// spins up the server. The guard asserts no EF migrations are defined and every mapped table exists.
builder.Services.AddSingleton<IStartupFilter, EfMigrationsDisabledStartupFilter>();

// --- Cross-cutting host concerns -------------------------------------------------------------
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Correlation-id middleware first (the API → bus → worker hop lines up in traces).
app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Admin CRUD endpoints — all require the admin-only policy.
app.MapAdminCatalogEndpoints();
app.MapProtectionEndpoints();
app.MapPhotoEndpoints();
app.MapMonetizationEndpoints();

await app.RunAsync();

/// <summary>Exposed so the WebApplicationFactory-based integration tests can boot the admin host.</summary>
public partial class Program;
