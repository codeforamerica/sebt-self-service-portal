using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SEBT.Portal.Kernel.Telemetry;

internal static class OpenTelemetrySetup
{
    internal const string ServiceName = "sebt-portal-api";

    public static void SetupOpenTelemetry(this WebApplicationBuilder builder)
    {
        var configSection = builder.Configuration.GetSection("Otel");

        // Use IConfiguration binding for AspNetCore instrumentation options.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(configSection.GetSection("AspNetCoreInstrumentation"));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: ServiceName,
                serviceInstanceId: Environment.MachineName
            // TODO serviceVersion: ???
            ))
            .WithTracing(tracingBuilder => ConfigureTracing(tracingBuilder, configSection))
            .WithMetrics(metricsBuilder => ConfigureMetrics(metricsBuilder, configSection));

        // OTLP log export is opt-in (Otel:UseLogExporter=otlp). When disabled we register no
        // OpenTelemetry logging provider at all and Serilog runs writeToProviders: false, so the
        // Serilog -> stdout -> CloudWatch path is completely unaffected by deploying this code.
        // When enabled, clear the framework's default logger providers first so that — with
        // Serilog's writeToProviders on — events are forwarded only to the OTLP exporter and are
        // not also duplicated onto stdout by the default console provider.
        if (GetConfiguredLogExporter(configSection) == "OTLP")
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddOpenTelemetry(ConfigureLogging);
        }

        builder.Services.AddSingleton<IInstrumentationSource, InstrumentationSource>();
    }

    private static void ConfigureLogging(OpenTelemetryLoggerOptions options)
    {
        ConfigureLoggingCommon(options);

        // Endpoint, protocol, and auth headers are supplied by the standard
        // OTEL_EXPORTER_OTLP_* / OTEL_EXPORTER_OTLP_LOGS_* environment variables, which the
        // exporter reads by default. This keeps secrets (e.g. an Authorization token) out of
        // appsettings and lets each environment point logs at its own OTLP target without a
        // code change.
        options.AddOtlpExporter();
    }

    /// <summary>
    /// Applies the log-exporter-agnostic OpenTelemetry logging options: the shared service
    /// resource and the record-content flags. Kept separate so the exporter can be swapped
    /// (e.g. for an in-memory exporter under test) without duplicating this configuration.
    /// </summary>
    internal static void ConfigureLoggingCommon(OpenTelemetryLoggerOptions options)
    {
        options.SetResourceBuilder(BuildLogsResourceBuilder());
        options.IncludeScopes = true;
        options.IncludeFormattedMessage = true;
    }

    /// <summary>
    /// Builds the resource that tags every exported log record, using the same
    /// <see cref="ServiceName"/> as traces and metrics so all three signals correlate.
    /// </summary>
    internal static ResourceBuilder BuildLogsResourceBuilder() =>
        ResourceBuilder.CreateDefault()
            .AddService(serviceName: ServiceName, serviceInstanceId: Environment.MachineName);

    /// <summary>
    /// Reads the configured log exporter. Defaults to <c>CONSOLE</c> so OTLP export is strictly
    /// opt-in and the CloudWatch logging path is never disrupted by merely deploying this code.
    /// </summary>
    internal static string GetConfiguredLogExporter(IConfiguration configSection) =>
        configSection.GetValue("UseLogExporter", defaultValue: "CONSOLE").ToUpperInvariant();

    private static void ConfigureTracing(TracerProviderBuilder tracingBuilder, IConfiguration configSection)
    {
        tracingBuilder
            .AddSource(InstrumentationSource.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        // Note: Switch between OTLP/Console by setting UseTracingExporter in appsettings.json.
        var tracingExporter = configSection.GetValue("UseTracingExporter", defaultValue: "CONSOLE").ToUpperInvariant();

        switch (tracingExporter)
        {
            case "OTLP":
                tracingBuilder.AddOtlpExporter(otlpOptions =>
                {
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint = new Uri(configSection.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317"));
                });
                break;

            default:
                tracingBuilder.AddConsoleExporter();
                break;
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metricsBuilder, IConfigurationSection configSection)
    {
        metricsBuilder
            .AddMeter(InstrumentationSource.MeterName)
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        // Note: Switch between Explicit/Exponential by setting HistogramAggregation in appsettings.json
        var histogramAggregation = configSection.GetValue("HistogramAggregation", defaultValue: "EXPLICIT").ToUpperInvariant();

        switch (histogramAggregation)
        {
            case "EXPONENTIAL":
                metricsBuilder.AddView(instrument =>
                {
                    return instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                        ? new Base2ExponentialBucketHistogramConfiguration()
                        : null;
                });
                break;
            default:
                // Explicit bounds histogram is the default.
                // No additional configuration necessary.
                break;
        }

        // Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
        var metricsExporter = configSection.GetValue("UseMetricsExporter", defaultValue: "CONSOLE").ToUpperInvariant();

        switch (metricsExporter)
        {
            case "OTLP":
                metricsBuilder.AddOtlpExporter(otlpOptions =>
                {
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint = new Uri(configSection.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317"));
                });
                break;
            default:
                metricsBuilder.AddConsoleExporter();
                break;
        }
    }
}
