using Microsoft.Extensions.Logging;
using Quartz;
using WhereToEat.Contracts.Geo;
using WhereToEat.Geo.Application;
using WhereToEat.Worker.Scheduling;

namespace WhereToEat.Worker.Jobs;

/// <summary>
/// (2) The geocode-refresh job (CLAUDE.md §5.2): it drains the queued geocode work — restaurants that
/// have an address but no stored coordinates yet (the catalog-owned "needs geocode" read on the
/// <see cref="ICatalogCoordinateWriter"/> seam) — and re-geocodes each through the Step 8
/// <see cref="GeocodeRestaurantCommandHandler"/> (OSM/Nominatim only — never a Google coordinate,
/// invariant #7). It is the scheduled driver of the same in-module reaction the <c>AddressChanged</c>
/// handler wires up; here it processes the backlog so a newly parsed venue gets coordinates on the
/// next run.
/// <para>
/// Idempotent and observable: a geocoded venue drops out of the next batch; one venue's geocode
/// failure is logged and skipped (best-effort, never aborts the batch); every step logs with the
/// Quartz fire-instance id as the correlation id. The per-run cap keeps a large backlog polite to
/// the Nominatim usage policy (the Step 8 adapter still enforces its own request interval).
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class GeocodeRefreshJob : IJob
{
    /// <summary>The maximum number of restaurants geocoded per run (politeness cap).</summary>
    public const int BatchSize = 100;

    /// <summary>The stable Quartz job key (its name + the worker job group).</summary>
    public static readonly JobKey Key = new("geocode-refresh", JobSchedule.JobGroup);

    private readonly ICatalogCoordinateWriter _coordinateWriter;
    private readonly GeocodeRestaurantCommandHandler _geocodeHandler;
    private readonly ILogger<GeocodeRefreshJob> _logger;

    public GeocodeRefreshJob(
        ICatalogCoordinateWriter coordinateWriter,
        GeocodeRestaurantCommandHandler geocodeHandler,
        ILogger<GeocodeRefreshJob> logger)
    {
        ArgumentNullException.ThrowIfNull(coordinateWriter);
        ArgumentNullException.ThrowIfNull(geocodeHandler);
        ArgumentNullException.ThrowIfNull(logger);
        _coordinateWriter = coordinateWriter;
        _geocodeHandler = geocodeHandler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.FireInstanceId;
        var cancellationToken = context.CancellationToken;

        var ids = await _coordinateWriter
            .GetRestaurantIdsNeedingGeocodeAsync(BatchSize, cancellationToken)
            .ConfigureAwait(false);

        LogStarting(correlationId, ids.Count);

        var refreshed = 0;
        foreach (var restaurantId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _geocodeHandler
                .HandleAsync(new GeocodeRestaurantCommand(restaurantId), cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                refreshed++;
            }
            else
            {
                LogGeocodeSkipped(correlationId, restaurantId, result.Error.Code);
            }
        }

        LogFinished(correlationId, refreshed, ids.Count);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Geocode refresh [{CorrelationId}] starting for {QueueCount} restaurant(s) needing coordinates.")]
    private partial void LogStarting(string correlationId, int queueCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Geocode refresh [{CorrelationId}] skipped restaurant {RestaurantId}: {ErrorCode}.")]
    private partial void LogGeocodeSkipped(string correlationId, Guid restaurantId, string errorCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Geocode refresh [{CorrelationId}] finished: {Refreshed}/{Total} restaurant(s) geocoded.")]
    private partial void LogFinished(string correlationId, int refreshed, int total);
}
