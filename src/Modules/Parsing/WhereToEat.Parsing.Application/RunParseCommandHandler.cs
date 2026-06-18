using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Application.Compliance;
using WhereToEat.Parsing.Domain;

namespace WhereToEat.Parsing.Application;

/// <summary>
/// Orchestrates the parse pipeline for one first-party source, in the strict order the design and the
/// invariants demand:
/// <list type="number">
///   <item><b>Compliance BEFORE any fetch</b> (invariant #9): first-party allow-list → robots.txt →
///   per-host rate-limit. Any gate that refuses ends the run <i>without</i> the strategy ever
///   touching the network.</item>
///   <item><b>Fetch</b> via the keyed <see cref="IRestaurantParser"/> (selected by
///   <see cref="SourceDescriptor.StrategyKey"/>).</item>
///   <item><b>Normalize</b> (invariant #2): mappable raw dishes → live items, unmappable → the
///   quarantine queue (never dropped, never fatal). The quarantine write happens regardless of the
///   admin-protection decision below, since it is review data, not a live menu write.</item>
///   <item><b>Admin-protected persist</b> (invariant #3): a <c>DoNotUpdate</c> restaurant is skipped
///   entirely; within an updatable one, any mapped item whose dish is flagged <c>DoNotParse</c> is
///   excluded before the write.</item>
///   <item><b>Geocode</b> (best-effort) the persisted restaurant through the Contracts seam.</item>
/// </list>
/// All cross-module work goes through <c>WhereToEat.Contracts</c> ports, so this handler never
/// references Catalog or Geo internals.
/// </summary>
public sealed partial class RunParseCommandHandler
{
    private readonly IFirstPartyAllowList _allowList;
    private readonly IRobotsTxtGate _robotsGate;
    private readonly IHostRateLimiter _rateLimiter;
    private readonly IParserStrategySelector _strategySelector;
    private readonly INormalizer _normalizer;
    private readonly IParseQuarantineStore _quarantineStore;
    private readonly ICatalogMenuWriter _menuWriter;
    private readonly IRestaurantGeocoder _geocoder;
    private readonly ILogger<RunParseCommandHandler> _logger;

    public RunParseCommandHandler(
        IFirstPartyAllowList allowList,
        IRobotsTxtGate robotsGate,
        IHostRateLimiter rateLimiter,
        IParserStrategySelector strategySelector,
        INormalizer normalizer,
        IParseQuarantineStore quarantineStore,
        ICatalogMenuWriter menuWriter,
        IRestaurantGeocoder geocoder,
        ILogger<RunParseCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(allowList);
        ArgumentNullException.ThrowIfNull(robotsGate);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(strategySelector);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(quarantineStore);
        ArgumentNullException.ThrowIfNull(menuWriter);
        ArgumentNullException.ThrowIfNull(geocoder);
        ArgumentNullException.ThrowIfNull(logger);
        _allowList = allowList;
        _robotsGate = robotsGate;
        _rateLimiter = rateLimiter;
        _strategySelector = strategySelector;
        _normalizer = normalizer;
        _quarantineStore = quarantineStore;
        _menuWriter = menuWriter;
        _geocoder = geocoder;
        _logger = logger;
    }

    /// <summary>Runs the pipeline for <paramref name="command"/>; see the type summary for the order.</summary>
    public async Task<RunParseResult> HandleAsync(RunParseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var source = command.Source;
        var host = source.Url.Host;

        // ---- 1. Compliance gates, BEFORE any fetch (invariant #9) --------------------------------
        if (!_allowList.IsFirstParty(host))
        {
            LogNotFirstParty(host);
            return new RunParseResult(RunParseOutcome.RejectedNotFirstParty);
        }

        if (!await _robotsGate.IsAllowedAsync(source.Url, cancellationToken).ConfigureAwait(false))
        {
            LogRobotsBlocked(source.Url);
            return new RunParseResult(RunParseOutcome.BlockedByRobots);
        }

        if (!await _rateLimiter.TryAcquireAsync(host, cancellationToken).ConfigureAwait(false))
        {
            LogRateLimited(host);
            return new RunParseResult(RunParseOutcome.BlockedByRateLimit);
        }

        // ---- 2. Fetch via the keyed strategy -----------------------------------------------------
        var strategyResult = _strategySelector.Resolve(source.StrategyKey);
        if (strategyResult.IsFailure)
        {
            LogParseFailed(source.RestaurantName, strategyResult.Error.Code, strategyResult.Error.Message);
            return new RunParseResult(RunParseOutcome.ParseFailed);
        }

        var parseResult = await strategyResult.Value.ParseAsync(source, cancellationToken).ConfigureAwait(false);
        if (parseResult.IsFailure)
        {
            LogParseFailed(source.RestaurantName, parseResult.Error.Code, parseResult.Error.Message);
            return new RunParseResult(RunParseOutcome.ParseFailed);
        }

        var menu = parseResult.Value;

        // ---- 3. Normalize: mapped vs. unmapped, and quarantine the unmapped (invariant #2) -------
        var normalization = await _normalizer.NormalizeAsync(menu, cancellationToken).ConfigureAwait(false);

        if (normalization.Quarantined.Count > 0)
        {
            // Quarantine is REVIEW data, not a live menu write — it is written regardless of the
            // admin-protection decision below so an unmappable dish is never lost (invariant #2).
            await _quarantineStore.AddAsync(normalization.Quarantined, cancellationToken).ConfigureAwait(false);
            LogQuarantined(source.RestaurantName, normalization.Quarantined.Count);
        }

        // ---- 4. Admin-protected persist (invariant #3) -------------------------------------------
        var protection = await _menuWriter
            .GetProtectionContextAsync(menu.Restaurant.Name, menu.Restaurant.AddressLine, cancellationToken)
            .ConfigureAwait(false);

        if (protection.DoNotUpdate)
        {
            // The whole venue is hand-curated; the parser must not touch it. Note the unmappable
            // items are STILL quarantined above — that is review data, not a write to the venue.
            LogDoNotUpdate(menu.Restaurant.Name);
            return new RunParseResult(
                RunParseOutcome.SkippedDoNotUpdate,
                QuarantinedItemCount: normalization.Quarantined.Count);
        }

        // Within an updatable venue, drop any mapped item whose dish is flagged DoNotParse — that
        // individual item is admin-curated and must not be created/overwritten by the parser.
        var clearedItems = new List<ResolvedMenuItemDto>(normalization.MappedItems.Count);
        var doNotParseSkipped = 0;
        foreach (var mapped in normalization.MappedItems)
        {
            if (protection.DoNotParseDishIds.Contains(mapped.DishId))
            {
                doNotParseSkipped++;
                continue;
            }

            clearedItems.Add(new ResolvedMenuItemDto(
                mapped.DishId,
                mapped.Price.Amount,
                mapped.Price.Currency,
                mapped.Weight));
        }

        var facts = new ParsedRestaurantFactsDto(
            menu.Restaurant.Name,
            menu.Restaurant.AddressLine,
            menu.Restaurant.City,
            menu.Restaurant.ContactLinks
                .Select(l => new ParsedContactLinkDto((int)l.Kind, l.Url, l.Label))
                .ToList());

        var restaurantId = await _menuWriter
            .PersistAsync(facts, clearedItems, cancellationToken)
            .ConfigureAwait(false);

        // ---- 5. Geocode (best-effort — a failure does not fail the parse) ------------------------
        var geocoded = await _geocoder.GeocodeAndStoreAsync(restaurantId, cancellationToken).ConfigureAwait(false);
        if (!geocoded)
        {
            LogGeocodeSkipped(restaurantId);
        }

        LogPersisted(menu.Restaurant.Name, clearedItems.Count, normalization.Quarantined.Count, doNotParseSkipped);
        return new RunParseResult(
            RunParseOutcome.Persisted,
            PersistedItemCount: clearedItems.Count,
            QuarantinedItemCount: normalization.Quarantined.Count,
            DoNotParseSkippedCount: doNotParseSkipped);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Parse rejected: host {Host} is not a first-party source (aggregator/disallowed).")]
    private partial void LogNotFirstParty(string host);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Parse blocked by robots.txt for {Url}.")]
    private partial void LogRobotsBlocked(Uri url);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Parse skipped: per-host rate-limit not yet elapsed for {Host}.")]
    private partial void LogRateLimited(string host);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Parse failed for {RestaurantName}: {ErrorCode} {ErrorMessage}.")]
    private partial void LogParseFailed(string restaurantName, string errorCode, string errorMessage);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Quarantined {Count} unmappable dish(es) from {RestaurantName} for admin review.")]
    private partial void LogQuarantined(string restaurantName, int count);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Parse skipped: restaurant {RestaurantName} is flagged DoNotUpdate (admin > parser).")]
    private partial void LogDoNotUpdate(string restaurantName);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Persisted {Persisted} menu item(s) for {RestaurantName} ({Quarantined} quarantined, {Skipped} DoNotParse-skipped).")]
    private partial void LogPersisted(string restaurantName, int persisted, int quarantined, int skipped);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information,
        Message = "Geocode produced no coordinate for restaurant {RestaurantId}; parse still persisted (best-effort).")]
    private partial void LogGeocodeSkipped(Guid restaurantId);
}
