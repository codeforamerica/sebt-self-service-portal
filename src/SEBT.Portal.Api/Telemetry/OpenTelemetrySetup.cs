using System.Diagnostics.Metrics;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SEBT.Portal.Kernel.Telemetry;

internal static class OpenTelemetrySetup
{
    private const string ServiceName = "sebt-portal-api";

    public static void SetupOpenTelemetry(this WebApplicationBuilder builder)
    {
        /* 
         * OTEL Logging is supported by .NET packages, but we are not currently
         * going to set this up. We're using Serilog for semantic logging, which
         * writes to stdout -> cloudwatch -> etc.
         *
         * If we ever move to OTEL-based logging, we'll set it up here.
         */
        // builder.Logging.AddOpenTelemetry(options =>
        // {
        //     options.SetResourceBuilder(
        //         ResourceBuilder.CreateDefault()
        //             .AddService(ServiceName));
        // });

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

        builder.Services.AddSingleton<IInstrumentationSource, InstrumentationSource>();
    }

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
