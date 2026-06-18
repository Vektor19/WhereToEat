using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace WhereToEat.BuildingBlocks.Observability;

/// <summary>
/// The cross-cutting observability wiring every host calls from its composition root: structured
/// logging (Serilog with a compact-JSON console sink, enriched with the correlation id from the log
/// scope), <b>OpenTelemetry</b> traces + metrics (ASP.NET Core / HttpClient / runtime instrumentation,
/// OTLP-exportable by config), and the correlation-id middleware/header so a request's id propagates
/// across API → bus → worker (see <see cref="CorrelationIdMiddleware"/>).
/// </summary>
public static class AddObservabilityExtensions
{
    /// <summary>
    /// Configures observability for a <b>web host</b> (public/admin API): Serilog logging, the ASP.NET
    /// correlation-id middleware, and OpenTelemetry with ASP.NET Core + HttpClient + MassTransit
    /// instrumentation. The host calls <see cref="UseCorrelationId"/> to insert the middleware.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        ConfigureSerilog(services, configuration, serviceName);

        // The middleware is resolved per-request; the host calls UseCorrelationId() to insert it.
        services.AddTransient<CorrelationIdMiddleware>();

        ConfigureOpenTelemetry(services, configuration, serviceName, includeAspNetCore: true);

        return services;
    }

    /// <summary>
    /// Configures observability for a <b>generic (non-web) host</b> such as the worker: Serilog logging
    /// and OpenTelemetry with HttpClient + runtime + MassTransit instrumentation, <b>without</b> the
    /// ASP.NET Core middleware/instrumentation (the worker has no HTTP request pipeline). The bus still
    /// propagates the correlation id via OpenTelemetry baggage, so a worker's consume log lines up with
    /// the request that published the message.
    /// </summary>
    public static IServiceCollection AddWorkerObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        ConfigureSerilog(services, configuration, serviceName);
        ConfigureOpenTelemetry(services, configuration, serviceName, includeAspNetCore: false);

        return services;
    }

    private static void ConfigureSerilog(IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(new CompactJsonFormatter())
            .CreateLogger();

        services.AddSerilog();
    }

    private static void ConfigureOpenTelemetry(
        IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCore)
    {
        // The exporter is config-gated: when OpenTelemetry:ConsoleExporter is true (local/dev) the
        // Console exporter is attached so traces/metrics are observable without a collector. A real
        // OTLP collector can be wired later (the instrumentation/resource/source config stays the same).
        var consoleExporter = configuration.GetValue<bool>("OpenTelemetry:ConsoleExporter");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                if (includeAspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource("MassTransit");

                if (consoleExporter)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                if (includeAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (consoleExporter)
                {
                    metrics.AddConsoleExporter();
                }
            });
    }

    /// <summary>
    /// Inserts the correlation-id middleware early in the request pipeline so every downstream log line
    /// and outbound publish carries the request's id. Call before the endpoints map.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
