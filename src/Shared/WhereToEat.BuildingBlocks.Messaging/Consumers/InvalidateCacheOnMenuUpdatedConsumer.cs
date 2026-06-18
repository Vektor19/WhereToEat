using MassTransit;
using Microsoft.Extensions.Logging;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.Contracts.IntegrationEvents;

namespace WhereToEat.BuildingBlocks.Messaging.Consumers;

/// <summary>
/// Reacts to a <see cref="MenuUpdated"/> integration event by invalidating the cache entries a menu
/// change can make stale: the price-median namespace (medians are keyed by selection+area, so a
/// targeted delete is not possible — the namespace is wiped and rebuilt lazily on the next miss) and
/// the dish-list-by-category entries are left to expire. It depends <b>only on
/// <c>WhereToEat.Contracts</c></b> (the event) and the <c>BuildingBlocks.Caching</c> abstraction — a
/// shared family, not a module — so the Step 2 consumer-boundary fitness rule (e) stays green.
/// <para>
/// <b>Privacy:</b> only our own derived caches are touched; no Google rating/coordinate is cached or
/// invalidated (those are never cached — CLAUDE.md #6/#7).
/// </para>
/// </summary>
public sealed partial class InvalidateCacheOnMenuUpdatedConsumer : IConsumer<MenuUpdated>
{
    private readonly ICacheService _cache;
    private readonly ILogger<InvalidateCacheOnMenuUpdatedConsumer> _logger;

    public InvalidateCacheOnMenuUpdatedConsumer(
        ICacheService cache,
        ILogger<InvalidateCacheOnMenuUpdatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<MenuUpdated> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A menu change moves prices, so every cached basket median may now be stale: wipe the median
        // namespace (rebuilt lazily). The taxonomy lists are also dropped since a new dish may have
        // appeared on this restaurant for a category.
        await _cache.RemoveByPrefixAsync(CacheKeys.PriceMedianPrefix, context.CancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveAsync(CacheKeys.CategoryList, context.CancellationToken).ConfigureAwait(false);

        LogInvalidated(context.Message.RestaurantId);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Invalidated price-median + taxonomy cache after a menu update on restaurant {RestaurantId}.")]
    private partial void LogInvalidated(Guid restaurantId);
}
