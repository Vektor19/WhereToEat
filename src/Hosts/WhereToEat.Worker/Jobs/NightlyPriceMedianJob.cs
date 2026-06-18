using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using WhereToEat.Recommendation.Infrastructure.Medians;
using WhereToEat.Worker.Scheduling;

namespace WhereToEat.Worker.Jobs;

/// <summary>
/// (3) The nightly price-median refresh job (CLAUDE.md §6 <c>f_price</c>): it recomputes the
/// per-area / per-category medians (city-wide fallback below threshold N) from the current
/// <c>catalog.MenuItem</c> prices and <b>writes them into the Step 7 price-median table</b> the
/// DB-only <see cref="DbPriceMedianProvider"/> reads. DB-only — NO Redis (Step 13 adds Redis as a
/// transparent caching decorator over the provider port, not over this writer). The recommendation
/// engine then reads a ready value per request; no median is ever computed on the request path.
/// <para>
/// Idempotent and observable: it replaces the table transactionally from the same source data, so a
/// retried run converges to the same rows; it logs the row count with the Quartz fire-instance id as
/// the correlation id.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class NightlyPriceMedianJob : IJob
{
    /// <summary>The stable Quartz job key (its name + the worker job group).</summary>
    public static readonly JobKey Key = new("nightly-price-median", JobSchedule.JobGroup);

    private readonly IPriceMedianWriter _writer;
    private readonly PriceMedianJobOptions _options;
    private readonly ILogger<NightlyPriceMedianJob> _logger;

    public NightlyPriceMedianJob(
        IPriceMedianWriter writer,
        IOptions<PriceMedianJobOptions> options,
        ILogger<NightlyPriceMedianJob> logger)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _writer = writer;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.FireInstanceId;

        LogStarting(correlationId, _options.AreaSampleThreshold);

        var rowsWritten = await _writer
            .RecomputeAsync(_options.AreaSampleThreshold, context.CancellationToken)
            .ConfigureAwait(false);

        LogFinished(correlationId, rowsWritten);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Nightly price-median [{CorrelationId}] starting (per-area threshold N = {Threshold}).")]
    private partial void LogStarting(string correlationId, int threshold);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Nightly price-median [{CorrelationId}] finished: {RowsWritten} median row(s) written.")]
    private partial void LogFinished(string correlationId, int rowsWritten);
}

/// <summary>
/// Options for the nightly median job: the per-area sample threshold N below which an area gets no row
/// and the recommendation engine falls back to the city-wide median. Bound from
/// <c>Worker:PriceMedian</c>; defaults to <see cref="PriceMedianCalculator.DefaultAreaSampleThreshold"/>.
/// </summary>
public sealed class PriceMedianJobOptions
{
    /// <summary>The appsettings section these options bind to.</summary>
    public const string SectionName = "Worker:PriceMedian";

    /// <summary>The per-area sample threshold N (>= 1); below it, the city-wide fallback row is used.</summary>
    public int AreaSampleThreshold { get; set; } = PriceMedianCalculator.DefaultAreaSampleThreshold;
}
