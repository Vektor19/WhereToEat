using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using WhereToEat.Worker.Jobs;
using WhereToEat.Worker.Scheduling;
using Xunit;

namespace WhereToEat.Worker.UnitTests;

/// <summary>
/// Unit tests for <see cref="JobSchedule"/>: the four jobs are registered with their stable keys and
/// the declared cron triggers (a regression here would silently change when a job runs). The schedule
/// is driven through a real in-memory Quartz scheduler built from the same <c>AddQuartz</c> config the
/// host uses (no DB store needed — the schedule is asserted, not executed).
/// </summary>
public sealed class JobScheduleTests
{
    [Fact]
    public void Cron_expressions_are_valid_quartz_crons()
    {
        // A malformed cron would throw here; this pins each declared expression as parseable.
        var crons = new[]
        {
            JobSchedule.WeeklyParseCron,
            JobSchedule.GeocodeRefreshCron,
            JobSchedule.NightlyPriceMedianCron,
            JobSchedule.RatingRecomputeCron,
        };

        foreach (var cron in crons)
        {
            CronExpression.IsValidExpression(cron).Should().BeTrue($"'{cron}' should be a valid cron");
        }
    }

    [Fact]
    public async Task Schedules_the_four_jobs_with_their_declared_crons()
    {
        var scheduler = await BuildSchedulerAsync();

        try
        {
            await AssertScheduled(scheduler, WeeklyParseJob.Key, JobSchedule.WeeklyParseCron);
            await AssertScheduled(scheduler, GeocodeRefreshJob.Key, JobSchedule.GeocodeRefreshCron);
            await AssertScheduled(scheduler, NightlyPriceMedianJob.Key, JobSchedule.NightlyPriceMedianCron);
            await AssertScheduled(scheduler, RatingRecomputeJob.Key, JobSchedule.RatingRecomputeCron);
        }
        finally
        {
            await scheduler.Shutdown(waitForJobsToComplete: false);
        }
    }

    [Fact]
    public async Task Registers_exactly_the_four_worker_jobs()
    {
        var scheduler = await BuildSchedulerAsync();

        try
        {
            var jobKeys = await scheduler.GetJobKeys(
                Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(JobSchedule.JobGroup));

            jobKeys.Should().HaveCount(4);
            jobKeys.Should().BeEquivalentTo(new[]
            {
                WeeklyParseJob.Key,
                GeocodeRefreshJob.Key,
                NightlyPriceMedianJob.Key,
                RatingRecomputeJob.Key,
            });
        }
        finally
        {
            await scheduler.Shutdown(waitForJobsToComplete: false);
        }
    }

    private static async Task AssertScheduled(IScheduler scheduler, JobKey key, string expectedCron)
    {
        (await scheduler.CheckExists(key)).Should().BeTrue($"job {key} should be registered");

        var triggers = await scheduler.GetTriggersOfJob(key);
        var trigger = triggers.Should().ContainSingle($"job {key} should have exactly one trigger").Subject;

        var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
        cronTrigger.CronExpressionString.Should().Be(expectedCron);
    }

    // Builds a real Quartz scheduler from the same JobSchedule config, but with the default in-memory
    // RAM store (no DB) — the schedule wiring is what is under test here, not the clustered store.
    private static async Task<IScheduler> BuildSchedulerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(JobSchedule.Configure);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await factory.GetScheduler();
        await scheduler.Start();
        return scheduler;
    }
}
