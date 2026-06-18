using System.Security.Cryptography;
using System.Text;
using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Anonymization;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Analytics.Infrastructure.Anonymization;

/// <summary>
/// The <see cref="IAnonymizer"/> implementation — the retain / hash / drop pipeline applied at ingest,
/// before persistence (§8.1 / invariant #11):
/// <list type="bullet">
///   <item><b>retain</b>: dish/category/restaurant ids, sort mode, filters, position go straight into
///   <see cref="EventDimensions"/>;</item>
///   <item><b>coarsen</b>: the time → an <see cref="HourBucket"/>; a precise lat/lng → a
///   neighbourhood-grade <see cref="Geohash"/> via <see cref="GeohashEncoder"/>;</item>
///   <item><b>hash</b>: a user/session id → a rotating-salted HMAC-SHA256 digest (<see cref="HashedActorId"/>);
///   the same id hashes stably within a salt window and differently across windows;</item>
///   <item><b>drop</b>: the raw id and the precise lat/lng are read locally and never returned —
///   the produced <see cref="AnalyticsEvent"/> cannot even represent them.</item>
/// </list>
/// </summary>
public sealed class Anonymizer : IAnonymizer
{
    private readonly ISaltProvider _saltProvider;
    private readonly AnonymizerOptions _options;

    public Anonymizer(ISaltProvider saltProvider, AnonymizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(saltProvider);
        ArgumentNullException.ThrowIfNull(options);
        _saltProvider = saltProvider;
        _options = options;
    }

    /// <inheritdoc />
    public Result<AnalyticsEvent> Anonymize(RawAnalyticsEvent raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        // --- retain: the non-identifying dimensions are kept verbatim ----------------------------
        var dimensions = EventDimensions.Create(
            restaurantId: raw.RestaurantId,
            categoryIds: raw.CategoryIds,
            dishIds: raw.DishIds,
            sortMode: raw.SortMode,
            filters: raw.Filters,
            position: raw.Position);

        // --- coarsen: time -> hour bucket (minute/second dropped) --------------------------------
        var hour = HourBucket.FromInstant(raw.OccurredAtUtc);

        // --- coarsen: precise lat/lng -> neighbourhood-grade geohash (precise coords dropped) -----
        var coarseLocation = TryCoarsenLocation(raw, out var geohashError);
        if (geohashError is not null)
        {
            return Result.Failure<AnalyticsEvent>(geohashError);
        }

        // --- hash: user/session id -> rotating-salted, non-reversible digest (raw id dropped) -----
        var actor = HashActor(raw);

        return AnalyticsEvent.Record(raw.Kind, hour, dimensions, actor, coarseLocation);
    }

    private Geohash? TryCoarsenLocation(RawAnalyticsEvent raw, out Error? error)
    {
        error = null;

        if (raw.Latitude is not { } lat || raw.Longitude is not { } lon)
        {
            // No opt-in location supplied — nothing to coarsen, nothing to drop.
            return null;
        }

        // Validate the precise coordinate before coarsening: an out-of-range value is bad input, not a
        // privacy event. We validate but never store the precise value.
        var point = GeoPoint.Create(lat, lon);
        if (point.IsFailure)
        {
            error = Error.Validation(
                "Analytics.InvalidLocation",
                "latitude must be within [-90, 90] and longitude within [-180, 180].");
            return null;
        }

        var encoded = GeohashEncoder.Encode(lat, lon, _options.EffectiveGeohashPrecision);

        var geohash = Geohash.Create(encoded);
        if (geohash.IsFailure)
        {
            // The encoder is capped at the domain precision floor, so this should be unreachable; if it
            // ever fires it is a coding error, surfaced rather than silently storing a bad value.
            error = geohash.Error;
            return null;
        }

        return geohash.Value;
    }

    private HashedActorId? HashActor(RawAnalyticsEvent raw)
    {
        // Prefer the user id; fall back to the session id. An actor-less event hashes to nothing.
        var rawId = !string.IsNullOrWhiteSpace(raw.UserId)
            ? raw.UserId
            : raw.SessionId;

        if (string.IsNullOrWhiteSpace(rawId))
        {
            return null;
        }

        var salt = _saltProvider.CurrentSalt;

        // HMAC-SHA256 keyed by the rotating salt: non-reversible and not brute-forceable without the
        // salt (which itself mixes the master secret). The raw id exists only in this local scope.
        var key = Encoding.UTF8.GetBytes(salt);
        var message = Encoding.UTF8.GetBytes(rawId);

        var digestBytes = HMACSHA256.HashData(key, message);
        var digest = Convert.ToHexString(digestBytes).ToLowerInvariant();

        var hashed = HashedActorId.FromDigest(digest);

        // A 64-char hex SHA-256 digest always satisfies the domain guard; this is defensive only.
        return hashed.IsSuccess ? hashed.Value : null;
    }
}
