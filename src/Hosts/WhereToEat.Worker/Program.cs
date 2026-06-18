using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using WhereToEat.BuildingBlocks.Migrations;
using WhereToEat.BuildingBlocks.Observability;
using WhereToEat.Worker;

var builder = Host.CreateApplicationBuilder(args);

// --- Observability (Step 13): structured logging + OpenTelemetry, no ASP.NET pipeline ---------
// The worker has no HTTP request pipeline, so it uses the generic-host observability overload; the
// correlation id still rides the bus baggage so a consumed message lines up with its publisher.
builder.Services.AddWorkerObservability(builder.Configuration, "WhereToEat.Worker");

// --- Configuration ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Catalog' — the worker needs the shared DB.");

// --- Schema-of-record migrations (incl. 0013 Quartz cluster tables) on startup ---------------
// The worker applies the same embedded scripts every host runs; DbUp journals them so this is a no-op
// after the first run. The clustered Quartz store needs the QRTZ_* tables present before it starts.
var migration = MigrationRunner.Run(connectionString);
if (!migration.Successful)
{
    throw new InvalidOperationException($"Worker startup migrations failed: {migration.ErrorMessage}");
}

// --- The worker composition root: modules + the clustered Quartz scheduler -------------------
builder.Services.AddWorkerServices(builder.Configuration, connectionString);

// Host Quartz inside the generic host; wait for jobs to finish on shutdown so a run is not torn off.
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();

/// <summary>Exposed so the worker integration tests can reference the host assembly's composition.</summary>
public partial class Program;
