using Microsoft.Extensions.Logging;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Abstractions;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Analytics.Application;

/// <summary>
/// Handles <see cref="IngestEventCommand"/>: it <b>anonymizes every raw event first</b> via the
/// <see cref="IAnonymizer"/> (retain/hash/drop — §8.1 / invariant #11) and only then appends the
/// resulting <see cref="AnalyticsEvent"/> aggregates through the append-optimized
/// <see cref="IAnalyticsEventStore"/> batch writer. No raw payload is ever handed to the store, so raw
/// PII / precise lat/lng cannot be persisted. A raw event that fails anonymization (e.g. an
/// out-of-range coordinate) is rejected without writing the batch — ingest is all-or-nothing per call.
/// </summary>
public sealed partial class IngestEventCommandHandler
{
    private readonly IAnonymizer _anonymizer;
    private readonly IAnalyticsEventStore _store;
    private readonly ILogger<IngestEventCommandHandler> _logger;

    public IngestEventCommandHandler(
        IAnonymizer anonymizer,
        IAnalyticsEventStore store,
        ILogger<IngestEventCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(anonymizer);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _anonymizer = anonymizer;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Anonymizes and appends the command's events. Returns a validation failure (and writes nothing) if
    /// any raw event cannot be anonymized; otherwise appends the anonymized batch in one round-trip.
    /// </summary>
    public async Task<Result> HandleAsync(IngestEventCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Events is null || command.Events.Count == 0)
        {
            return Result.Failure(
                Error.Validation("Analytics.NoEvents", "At least one analytics event is required."));
        }

        var anonymized = new List<AnalyticsEvent>(command.Events.Count);
        foreach (var raw in command.Events)
        {
            // Anonymize BEFORE persistence: the raw id / precise coordinate live only here, in memory,
            // and are dropped/hashed/coarsened into the domain aggregate. A failure short-circuits the
            // whole batch so nothing partial is written.
            var result = _anonymizer.Anonymize(raw);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            anonymized.Add(result.Value);
        }

        await _store.AppendBatchAsync(anonymized, cancellationToken).ConfigureAwait(false);

        LogIngested(anonymized.Count);
        return Result.Success();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Ingested {EventCount} anonymized analytics event(s).")]
    private partial void LogIngested(int eventCount);
}
