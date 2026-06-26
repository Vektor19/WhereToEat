using WhereToEat.Analytics.Infrastructure.DependencyInjection;
using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.BuildingBlocks.Messaging;
using WhereToEat.BuildingBlocks.Observability;
using WhereToEat.Catalog.Infrastructure.DependencyInjection;
using WhereToEat.Catalog.Search.Application.DependencyInjection;
using WhereToEat.Geo.Infrastructure;
using WhereToEat.PublicApi.Endpoints;
using WhereToEat.Ratings.Infrastructure.DependencyInjection;
using WhereToEat.Recommendation.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// --- Observability first so every later registration logs through Serilog (Step 13) ----------
// Structured JSON logging + correlation ids + OpenTelemetry traces/metrics, wired in the
// composition root so a request's id propagates across the public API → bus → worker hop.
builder.Services.AddObservability(builder.Configuration, "WhereToEat.PublicApi");

// --- Configuration ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Catalog' — the public host needs the catalog DB.");

// --- Modules (one AddXxxModule per module, the composition convention) -----------------------
// Catalog provides the persistence + read queries; Catalog.Search reuses the same repository for
// its deterministic prefix lookups. No Geo/geocoder module is wired here — the Map endpoint is a
// DB-only stub this step (the geocoder port arrives in Step 8).
builder.Services.AddCatalogModule(connectionString);
builder.Services.AddCatalogSearchModule();

// The recommendation engine (Step 7): pluggable match/sort/filter strategies resolved by key
// (invariant #4), the DB-only price-median provider, and the candidate source joining the catalog
// read model + the materialized rating aggregate (the smoothed rating crosses only as a DTO field).
builder.Services.AddRecommendationModule(connectionString);

// The analytics ingest module (Step 11): the rotating-salt anonymizer runs at ingest BEFORE
// persistence (retain/hash/drop — invariant #11), the append-optimized writer persists only the
// anonymized event, and the rollup reader exposes aggregates only. The salt master secret comes from
// config so a stored hash is not brute-forceable from the public salt alone; a blank secret fails
// fast outside Dev/Testing (gated on the environment name, like the OIDC authority guard).
builder.Services.AddAnalyticsModule(connectionString, builder.Environment.EnvironmentName, options =>
{
    var masterSecret = builder.Configuration["Analytics:SaltMasterSecret"];
    if (!string.IsNullOrWhiteSpace(masterSecret))
    {
        options.MasterSecret = masterSecret;
    }
});

// --- OIDC resource-server auth (validate external IdP tokens; we never issue tokens) ---------
// The environment name gates the Dev/Testing-only test-signing-key seam and the fail-fast on a
// blank production Authority — production validates ONLY against the IdP JWKS (issuer/audience/
// lifetime on), and a leaked test key cannot reach the validation path outside Dev/Testing.
builder.Services.AddOidcResourceServerAuth(builder.Configuration, builder.Environment.EnvironmentName);

// --- Step 13 cross-cutting infrastructure ----------------------------------------------------
// Redis cache behind ICacheService (NullCacheService when Redis is disabled — Step 7 stays correct
// either way; the median provider transparently decorates with CachingPriceMedianProvider when a
// real cache is present). MassTransit carries the Contracts integration events (in-process now,
// RabbitMQ by config). The Geo module supplies the IRestaurantGeocoder adapter the bus's
// geocode-on-AddressChanged consumer resolves; Catalog (above) supplies the coordinate-writer seam.
builder.Services.AddRedisCache(builder.Configuration);
builder.Services.AddGeoModule(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);

// Step 22: the Ratings write path (the authenticated POST rating endpoint). AddRatingsApplication —
// NOT AddRatingsPersistence (that is the Worker host's aggregate-recompute path) — registers the
// IRatingRepository write port + Dapper adapter + the SubmitRatingCommandHandler + the MassTransit-backed
// RatingGiven publisher. It is wired AFTER AddMessaging because the publisher resolves IPublishEndpoint.
builder.Services.AddRatingsApplication(connectionString);

// --- Cross-cutting host concerns -------------------------------------------------------------
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// The correlation-id middleware runs first so every downstream log line + outbound bus publish
// carries the request's id (the API → bus → worker hop lines up in traces).
app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Public, anonymous read endpoints (invariant #1: deterministic, list/prefix selection — no NLP).
app.MapCatalogEndpoints();
app.MapSearchEndpoints();
app.MapMapEndpoints();
app.MapRecommendEndpoints();

// Analytics ingest (anonymized at ingest, before persistence — invariant #11). Anonymous.
app.MapAnalyticsEndpoints();

// The authenticated seam (the read endpoints above stay anonymous).
app.MapAuthProbeEndpoints();

// Step 22: the authenticated rating-submit endpoint (POST /restaurants/{id}/ratings) — 401 anonymous,
// maps the IdP sub -> an opaque user ref, drives the submit use-case, publishes RatingGiven (invariant #6).
app.MapRatingEndpoints();

app.Run();

/// <summary>Exposed so the WebApplicationFactory-based integration tests can boot the host.</summary>
public partial class Program;
