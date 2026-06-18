using Microsoft.Extensions.Logging;
using Quartz;
using WhereToEat.Ratings.Infrastructure.Persistence;
using WhereToEat.Worker.Scheduling;

namespace WhereToEat.Worker.Jobs;

/// <summary>
/// (4) The rating-recompute job (CLAUDE.md §5.6 / invariant #6): it refreshes the materialized Step 5
/// per-restaurant rating aggregate (cumulative all-time sum + count) from the raw <c>ratings.Rating</c>
/// facts via the Ratings module's <see cref="IRatingAggregateRecomputer"/>. The smoothed value the
/// recommendation candidate source ranks on is the single authoritative <c>BayesianRatingSmoothing</c>
/// over those two materialized numbers, so the rollup this job writes always matches the pure-domain
/// formula (the engine and the job never diverge).
/// <para>
/// Idempotent and observable: it recomputes from the same raw facts and upserts the same rows, so a
/// retried run converges; it logs the aggregate count with the Quartz fire-instance id as the
/// correlation id.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class RatingRecomputeJob : IJob
{
    /// <summary>The stable Quartz job key (its name + the worker job group).</summary>
    public static readonly JobKey Key = new("rating-recompute", JobSchedule.JobGroup);

    private readonly IRatingAggregateRecomputer _recomputer;
    private readonly ILogger<RatingRecomputeJob> _logger;

    public RatingRecomputeJob(IRatingAggregateRecomputer recomputer, ILogger<RatingRecomputeJob> logger)
    {
        ArgumentNullException.ThrowIfNull(recomputer);
        ArgumentNullException.ThrowIfNull(logger);
        _recomputer = recomputer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.FireInstanceId;

        LogStarting(correlationId);

        var aggregatesWritten = await _recomputer
            .RecomputeAllAsync(context.CancellationToken)
            .ConfigureAwait(false);

        LogFinished(correlationId, aggregatesWritten);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Rating recompute [{CorrelationId}] starting.")]
    private partial void LogStarting(string correlationId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Rating recompute [{CorrelationId}] finished: {AggregatesWritten} aggregate(s) materialized.")]
    private partial void LogFinished(string correlationId, int aggregatesWritten);
}
