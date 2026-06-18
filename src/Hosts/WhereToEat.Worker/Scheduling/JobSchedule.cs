using Quartz;
using WhereToEat.Worker.Jobs;

namespace WhereToEat.Worker.Scheduling;

/// <summary>
/// The cron wiring for the worker's four background jobs (the single place schedules are declared, so
/// they are reviewable as a unit and unit-testable). Each job is registered with a stable identity and
/// a cron trigger whose misfire policy fires the missed occurrence once on recovery (rather than
/// firing every skipped window or doing nothing) — appropriate for periodic maintenance jobs under the
/// clustered store, where a replica may have been down when the trigger was due.
/// <para>
/// Cron format is Quartz's 7-field "seconds minutes hours day-of-month month day-of-week [year]".
/// </para>
/// </summary>
public static class JobSchedule
{
    /// <summary>The Quartz job/trigger group every worker job + trigger belongs to.</summary>
    public const string JobGroup = "wheretoeat-worker";

    /// <summary>Weekly parse: Mondays at 02:00 (CLAUDE.md §5.1 "once a week").</summary>
    public const string WeeklyParseCron = "0 0 2 ? * MON";

    /// <summary>Geocode refresh: hourly at minute 15 (drains the geocode backlog between parses).</summary>
    public const string GeocodeRefreshCron = "0 15 * * * ?";

    /// <summary>Nightly price-median refresh: every day at 03:00 (after the data settles overnight).</summary>
    public const string NightlyPriceMedianCron = "0 0 3 * * ?";

    /// <summary>Nightly rating recompute: every day at 03:30 (after the median refresh).</summary>
    public const string RatingRecomputeCron = "0 30 3 * * ?";

    /// <summary>
    /// Registers the four jobs and their cron triggers on <paramref name="quartz"/>. Called from the
    /// worker's composition root inside <c>AddQuartz</c>; the clustered/persistent store config lives
    /// there, while the schedule (what runs and when) lives here.
    /// </summary>
    public static void Configure(IServiceCollectionQuartzConfigurator quartz)
    {
        ArgumentNullException.ThrowIfNull(quartz);

        AddCronJob<WeeklyParseJob>(quartz, WeeklyParseJob.Key, WeeklyParseCron);
        AddCronJob<GeocodeRefreshJob>(quartz, GeocodeRefreshJob.Key, GeocodeRefreshCron);
        AddCronJob<NightlyPriceMedianJob>(quartz, NightlyPriceMedianJob.Key, NightlyPriceMedianCron);
        AddCronJob<RatingRecomputeJob>(quartz, RatingRecomputeJob.Key, RatingRecomputeCron);
    }

    private static void AddCronJob<TJob>(
        IServiceCollectionQuartzConfigurator quartz,
        JobKey key,
        string cron)
        where TJob : IJob
    {
        quartz.AddJob<TJob>(job => job
            .WithIdentity(key)
            // Durable so the job definition survives in the persistent store even with no live trigger.
            .StoreDurably()
            // On recovery (a replica died mid-run under the cluster) the job is re-run, not lost.
            .RequestRecovery());

        quartz.AddTrigger(trigger => trigger
            .ForJob(key)
            .WithIdentity($"{key.Name}-trigger", JobGroup)
            .WithCronSchedule(cron, cronOptions => cronOptions
                // A missed periodic run fires once on recovery — not every skipped window, not never.
                .WithMisfireHandlingInstructionFireAndProceed()));
    }
}
