using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Domain;

namespace WhereToEat.Parsing.Infrastructure;

/// <summary>
/// The concrete <see cref="INormalizer"/> (invariant #2): it maps each raw dish line onto the canonical
/// Category → Dish taxonomy through the <c>Contracts</c> <see cref="ICatalogDishResolver"/> seam (a
/// <b>deterministic</b> normalized lookup — no NLP, invariant #1) and returns the
/// <see cref="NormalizationResult"/> mapped/unmapped split. A raw name the resolver cannot match is
/// <b>not</b> dropped and does <b>not</b> fail the parse: it becomes a <see cref="ParseQuarantineItem"/>
/// for later admin review, while every mappable line in the same menu still normalizes. Units/prices
/// are already unified into <c>Money</c> by the strategy, so the normalizer only carries them through.
/// Time comes from an injected <see cref="TimeProvider"/> so the detection timestamp is deterministic
/// in tests.
/// </summary>
public sealed partial class Normalizer : INormalizer
{
    private readonly ICatalogDishResolver _dishResolver;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Normalizer> _logger;

    public Normalizer(ICatalogDishResolver dishResolver, TimeProvider timeProvider, ILogger<Normalizer> logger)
    {
        ArgumentNullException.ThrowIfNull(dishResolver);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _dishResolver = dishResolver;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NormalizationResult> NormalizeAsync(ParsedMenu menu, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(menu);

        var mapped = new List<NormalizedMenuItem>(menu.Dishes.Count);
        var quarantined = new List<ParseQuarantineItem>();
        var detectedAt = _timeProvider.GetUtcNow();

        foreach (var dish in menu.Dishes)
        {
            var canonical = await _dishResolver
                .ResolveAsync(dish.RawName, dish.CategoryHint, cancellationToken)
                .ConfigureAwait(false);

            if (canonical is null)
            {
                // Unmappable -> quarantine (never dropped, never fatal — invariant #2).
                quarantined.Add(ParseQuarantineItem.ForUnmapped(
                    menu.Restaurant.Name,
                    menu.Restaurant.AddressLine,
                    dish,
                    detectedAt));
                LogQuarantined(dish.RawName, menu.Restaurant.Name);
                continue;
            }

            mapped.Add(new NormalizedMenuItem(canonical.DishId, dish.Price, dish.Weight));
        }

        return new NormalizationResult(mapped, quarantined);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Quarantined unmappable dish '{RawName}' from {RestaurantName} (no canonical match).")]
    private partial void LogQuarantined(string rawName, string restaurantName);
}
