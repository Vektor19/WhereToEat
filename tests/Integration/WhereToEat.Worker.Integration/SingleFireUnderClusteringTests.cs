using System.Collections.Specialized;
using FluentAssertions;
using Quartz;
using Quartz.Impl;
using Xunit;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// The key Quartz-clustering proof (Step 12): TWO scheduler instances pointed at the SAME SQL Server
/// job store form one cluster, and a scheduled trigger fires on <b>exactly ONE</b> instance. The
/// <see cref="CountingJob"/> records executions in a shared counter; after both instances run, the
/// count must be exactly 1 — the clustered, DB-backed store elected a single node for the fire.
/// <para>
/// Made deterministic (not flaky): the job fires <b>once</b> (a one-shot SimpleTrigger, no repeat), and
/// the test polls the counter up to a generous timeout, then waits a further settle window to confirm
/// the OTHER instance does not also fire. Both schedulers use auto-generated instance ids so they are
/// distinct cluster members of the same scheduler name.
/// </para>
/// </summary>
[Collection("worker-sql")]
public sealed class SingleFireUnderClusteringTests
{
    private readonly WorkerSqlServerFixture _fixture;

    public SingleFireUnderClusteringTests(WorkerSqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_scheduled_trigger_fires_on_exactly_one_clustered_instance()
    {
        CountingJob.Reset();

        var schedulerA = await BuildClusteredSchedulerAsync("instance-A");
        var schedulerB = await BuildClusteredSchedulerAsync("instance-B");

        try
        {
            await schedulerA.Start();
            await schedulerB.Start();

            // Schedule the one-shot job ONCE in the shared store (from instance A). Both clustered
            // instances see it through the DB; the cluster elects exactly one to fire it.
            var job = JobBuilder.Create<CountingJob>()
                .WithIdentity("counting-job", "clustering-test")
                .StoreDurably()
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity("counting-trigger", "clustering-test")
                .ForJob(job)
                .StartAt(DateBuilder.FutureDate(2, IntervalUnit.Second))
                .Build();

            await schedulerA.ScheduleJob(job, trigger);

            // Poll until the job has fired (up to ~30s), then settle to confirm no second fire.
            await WaitUntilAsync(() => CountingJob.ExecutionCount >= 1, TimeSpan.FromSeconds(30));
            await Task.Delay(TimeSpan.FromSeconds(5));

            CountingJob.ExecutionCount.Should().Be(1,
                "a clustered, DB-backed store must fire a scheduled trigger on exactly one instance");
        }
        finally
        {
            await schedulerA.Shutdown(waitForJobsToComplete: true);
            await schedulerB.Shutdown(waitForJobsToComplete: true);
        }
    }

    private async Task<IScheduler> BuildClusteredSchedulerAsync(string instanceName)
    {
        // Configure the standard AdoJobStore in CLUSTERED mode over the migrated SQL Server (the QRTZ_*
        // cluster tables come from migration 0013). A shared SchedulerName makes both schedulers members
        // of ONE cluster; a distinct InstanceName makes each a distinct member.
        var properties = new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = "WhereToEatWorkerScheduler",
            ["quartz.scheduler.instanceId"] = instanceName,
            ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
            ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SqlServerDelegate, Quartz",
            ["quartz.jobStore.dataSource"] = "default",
            ["quartz.jobStore.tablePrefix"] = "QRTZ_",
            ["quartz.jobStore.clustered"] = "true",
            ["quartz.jobStore.clusterCheckinInterval"] = "5000",
            ["quartz.jobStore.useProperties"] = "true",
            ["quartz.dataSource.default.connectionString"] = _fixture.ConnectionString,
            ["quartz.dataSource.default.provider"] = "SqlServer",
            // serializer.type is REQUIRED only on this raw StdSchedulerFactory path: the bare factory
            // does not default it for a non-RAM job store. Production (WorkerCompositionRoot.AddClusteredQuartz)
            // goes through the AddQuartz DI builder, which supplies the same binary serializer implicitly —
            // so "binary" here is the explicit equivalent of production's implicit default, not a divergence.
            ["quartz.serializer.type"] = "binary",
        };

        var factory = new StdSchedulerFactory(properties);
        return await factory.GetScheduler();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(250);
        }
    }
}
