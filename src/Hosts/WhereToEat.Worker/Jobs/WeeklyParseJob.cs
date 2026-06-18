using Microsoft.Extensions.Logging;
using Quartz;
using WhereToEat.Parsing.Application;
using WhereToEat.Worker.Scheduling;

namespace WhereToEat.Worker.Jobs;

/// <summary>
/// (1) The weekly parse job (CLAUDE.md §5.1): for each configured first-party source it delegates to
/// the Step 9 <see cref="RunParseCommandHandler"/>, which already enforces the compliance gates BEFORE
/// any fetch (first-party allow-list / robots.txt / rate-limit — invariant #9) and the admin-protected
/// persist (a <c>DoNotUpdate</c> venue is skipped entirely; a <c>DoNotParse</c> item is not overwritten
/// — invariant #3, admin &gt; parser). The job adds no parsing logic of its own; it is purely the
/// schedule + iteration shell.
/// <para>
/// Idempotent and observable: <see cref="DisallowConcurrentExecutionAttribute"/> stops a slow run from
/// overlapping itself, each source is processed independently (one failure does not abort the batch),
/// and every step logs with the Quartz fire-instance id as the correlation id.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class WeeklyParseJob : IJob
{
    /// <summary>The stable Quartz job key (its name + the worker job group).</summary>
    public static readonly JobKey Key = new("weekly-parse", JobSchedule.JobGroup);

    private readonly IParseSourceProvider _sourceProvider;
    private readonly RunParseCommandHandler _parseHandler;
    private readonly ILogger<WeeklyParseJob> _logger;

    public WeeklyParseJob(
        IParseSourceProvider sourceProvider,
        RunParseCommandHandler parseHandler,
        ILogger<WeeklyParseJob> logger)
    {
        ArgumentNullException.ThrowIfNull(sourceProvider);
        ArgumentNullException.ThrowIfNull(parseHandler);
        ArgumentNullException.ThrowIfNull(logger);
        _sourceProvider = sourceProvider;
        _parseHandler = parseHandler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.FireInstanceId;
        var cancellationToken = context.CancellationToken;

        var sources = _sourceProvider.GetSources();
        LogStarting(correlationId, sources.Count);

        var succeeded = 0;
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _parseHandler
                    .HandleAsync(new RunParseCommand(source), cancellationToken)
                    .ConfigureAwait(false);
                LogSourceProcessed(correlationId, source.RestaurantName, result.Outcome.ToString());
                succeeded++;
            }
#pragma warning disable CA1031 // One source's failure must not abort the weekly batch — log and continue.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogSourceFailed(correlationId, source.RestaurantName, ex);
            }
        }

        LogFinished(correlationId, succeeded, sources.Count);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Weekly parse [{CorrelationId}] starting for {SourceCount} first-party source(s).")]
    private partial void LogStarting(string correlationId, int sourceCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Weekly parse [{CorrelationId}] processed {RestaurantName}: {Outcome}.")]
    private partial void LogSourceProcessed(string correlationId, string restaurantName, string outcome);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Weekly parse [{CorrelationId}] failed for {RestaurantName}; continuing with the rest.")]
    private partial void LogSourceFailed(string correlationId, string restaurantName, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Weekly parse [{CorrelationId}] finished: {Succeeded}/{Total} source(s) processed.")]
    private partial void LogFinished(string correlationId, int succeeded, int total);
}
