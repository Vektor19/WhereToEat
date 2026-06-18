using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereToEat.BuildingBlocks.Caching;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// The "never cache Google ratings/coordinates" guard (CLAUDE.md invariants #6/#7, §5.7). The
/// promise in <see cref="ICacheService"/>'s docs is enforced mechanically here, two ways:
/// <list type="number">
/// <item>a static inspection asserting <see cref="CacheKeys"/> defines no Google-shaped key — so no
/// Google-sourced value can even be addressed in the cache;</item>
/// <item>a recording <see cref="ICacheService"/> spy wired through the real Map endpoint path (the
/// endpoint that handles Place ID / deep-link) asserting the cache is <b>never</b> touched — the map
/// payload is DB-only and live, never cached.</item>
/// </list>
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class CachePrivacyGuardTests
{
    private readonly PublicApiFixture _fixture;

    public CachePrivacyGuardTests(PublicApiFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Tokens that betray a Google-sourced value being keyed into the cache. The cache key surface is
    /// the only place a "what we cache" decision is encoded as a string, so scanning it catches a
    /// future key like <c>google:rating:...</c> before it can ship.
    /// </summary>
    private static readonly string[] ForbiddenKeyTokens =
    {
        "google",
        "gmaps",
        "googlerating",
        "google-rating",
    };

    [Fact]
    public void CacheKeys_DefineNoGoogleSourcedKey()
    {
        // Every const string field + every key-building method on CacheKeys is inspected. Methods are
        // exercised with placeholder arguments so a forbidden token baked into a built key (not just a
        // const) is also caught.
        var keys = StaticCacheKeyStrings().ToList();

        keys.Should().NotBeEmpty("CacheKeys must expose at least the taxonomy/median/rating keys");

        foreach (var key in keys)
        {
            foreach (var token in ForbiddenKeyTokens)
            {
                key.ToLowerInvariant().Should().NotContain(
                    token,
                    "no cache key may derive from a Google-sourced rating/coordinate (CLAUDE.md #6/#7); " +
                    "found a key '{0}' containing '{1}'",
                    key,
                    token);
            }
        }
    }

    [Fact]
    public async Task MapEndpoint_NeverTouchesTheCache()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var spy = new RecordingCacheService();

        // A host that reuses the fixture's container/DB + test signing key but swaps in the recording
        // cache so any cache access from the Map path is observable. ConfigureTestServices runs after
        // the host's own registrations, so Replace wins over the real ICacheService singleton.
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Singleton<ICacheService>(spy))));

        var client = factory.CreateClient();

        // The Map path for the restaurant that has stored coordinates + Place ID + deep-link — exactly
        // the payload §5.7 says is DB-only/live and must never be cached.
        var response = await client.GetAsync(
            new Uri($"/map/{seed.RestaurantWithCoordsId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        spy.Interactions.Should().BeEmpty(
            "the Map payload (Place ID / coordinates) is served live from the DB and must NEVER be " +
            "cached (§5.7 / invariant #6/#7); the cache was touched: {0}",
            string.Join(", ", spy.Interactions));
    }

    // Reads every public const string and invokes every public static key-building method (with
    // placeholder args matching its parameter types) so both literal and computed keys are inspected.
    private static IEnumerable<string> StaticCacheKeyStrings()
    {
        foreach (var field in typeof(CacheKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.FieldType == typeof(string)
                && field.GetRawConstantValue() is string literal)
            {
                yield return literal;
            }
        }

        foreach (var method in typeof(CacheKeys).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.ReturnType != typeof(string))
            {
                continue;
            }

            var args = method.GetParameters().Select(p => PlaceholderFor(p.ParameterType)).ToArray();
            if (method.Invoke(null, args) is string built)
            {
                yield return built;
            }
        }
    }

    private static object PlaceholderFor(Type type)
    {
        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(string))
        {
            return "placeholder";
        }

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unsupported CacheKeys parameter type: {type}");
    }

    /// <summary>
    /// An <see cref="ICacheService"/> that records every call and otherwise behaves as a permanent miss
    /// (so the path under test runs normally). Any recorded interaction on the Map path is a violation.
    /// </summary>
    private sealed class RecordingCacheService : ICacheService
    {
        private readonly List<string> _interactions = new();

        public IReadOnlyList<string> Interactions => _interactions;

        public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            _interactions.Add($"Get({key})");
            return Task.FromResult<string?>(null);
        }

        public Task SetStringAsync(
            string key,
            string value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            _interactions.Add($"Set({key})");
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _interactions.Add($"Remove({key})");
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            _interactions.Add($"RemoveByPrefix({prefix})");
            return Task.CompletedTask;
        }

        public Task<bool> TryClaimSlotAsync(
            string key,
            TimeSpan interval,
            CancellationToken cancellationToken = default)
        {
            _interactions.Add($"TryClaimSlot({key})");
            return Task.FromResult(true);
        }
    }
}
