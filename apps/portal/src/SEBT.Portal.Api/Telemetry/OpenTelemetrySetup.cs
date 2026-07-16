using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel.Telemetry;

internal static class OpenTelemetrySetup
{
    internal const string ServiceName = "sebt-portal-api";

    public static void SetupOpenTelemetry(this WebApplicationBuilder builder)
    {
        var configSection =
            builder.Configuration.GetSection(OpenTelemetrySettings.SectionName);

        builder.Services
            .AddOptions<OpenTelemetrySettings>()
            .Bind(configSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Use IConfiguration binding for AspNetCore instrumentation and OTLP Exporter options.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(configSection.GetSection("AspNetCoreInstrumentation"));
        builder.Services.Configure<OtlpExporterOptions>(configSection.GetSection("OtlpExporter"));

        // Get the OtelSettings instance from configuration, or use defaults if not present.
        var otelSettings = configSection
            .Get<OpenTelemetrySettings>() ?? new OpenTelemetrySettings();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: ServiceName,
                serviceInstanceId: Environment.MachineName
            // TODO serviceVersion: ???
            ))
            .WithTracing(tracingBuilder => ConfigureTracing(tracingBuilder, otelSettings))
            .WithMetrics(metricsBuilder => ConfigureMetrics(metricsBuilder, otelSettings));

        // OTLP log export is opt-in (Otel:UseLogExporter=otlp). When disabled we register no
        // OpenTelemetry logging provider at all and Serilog runs writeToProviders: false, so the
        // Serilog -> stdout -> CloudWatch path is completely unaffected by deploying this code.
        if (otelSettings.UseLogExporter == ExporterKind.Otlp)
        {
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

    private static void ConfigureTracing(TracerProviderBuilder tracingBuilder, OpenTelemetrySettings otelSettings)
    {
        tracingBuilder
            .AddSource(InstrumentationSource.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        switch (otelSettings.UseTracingExporter)
        {
            case ExporterKind.Otlp:
                tracingBuilder.AddOtlpExporter();
                break;

            default:
                tracingBuilder.AddConsoleExporter();
                break;
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metricsBuilder, OpenTelemetrySettings otelSettings)
    {
        metricsBuilder
            .AddMeter(InstrumentationSource.MeterName)
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        switch (otelSettings.HistogramAggregation)
        {
            case HistogramAggregationKind.Exponential:
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
        switch (otelSettings.UseMetricsExporter)
        {
            case ExporterKind.Otlp:
                metricsBuilder.AddOtlpExporter();
                break;
            default:
                metricsBuilder.AddConsoleExporter();
                break;
        }
    }
}
