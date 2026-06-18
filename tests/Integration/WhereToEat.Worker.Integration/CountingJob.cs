using Quartz;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// A trivial Quartz job that records every execution in a process-wide counter — the instrument the
/// single-fire-under-clustering test uses to prove a scheduled trigger fires on exactly ONE of two
/// clustered scheduler instances. <see cref="DisallowConcurrentExecutionAttribute"/> mirrors the real
/// jobs' idempotency posture; the counter is shared because both schedulers run in the same test
/// process (each is a distinct cluster member, but they share this in-memory tally).
/// </summary>
[DisallowConcurrentExecution]
public sealed class CountingJob : IJob
{
    private static int _executionCount;

    /// <summary>The number of times any clustered instance has executed this job.</summary>
    public static int ExecutionCount => Volatile.Read(ref _executionCount);

    /// <summary>Resets the counter between tests.</summary>
    public static void Reset() => Volatile.Write(ref _executionCount, 0);

    /// <inheritdoc />
    public Task Execute(IJobExecutionContext context)
    {
        Interlocked.Increment(ref _executionCount);
        return Task.CompletedTask;
    }
}
