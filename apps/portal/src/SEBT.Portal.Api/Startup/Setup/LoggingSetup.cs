using SEBT.Portal.Api.Telemetry;
using Serilog;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class LoggingSetup
{
    public static WebApplicationBuilder SetupSerilog(this WebApplicationBuilder builder)
    {
        // Console sink is configured in code (not appsettings) so we can use
        // human-readable text locally and structured JSON in deployed environments.
        // Field names match Datadog's reserved attributes (`date`, `status`,
        // `message`) so they are auto-recognized without configuring a per-service
        // log pipeline. Without these names the Forwarder Lambda falls back to the
        // CloudWatch event time for the timeline and tags the log with
        // `service:cloudwatch`. The literal service value must match the OTEL
        // ServiceName constant in OpenTelemetrySetup so traces and logs correlate
        // under the same service in Datadog.
        // Set LOG_FORMAT=json in ECS task definitions to enable structured output.
        var useJsonLogs = string.Equals(
            Environment.GetEnvironmentVariable("LOG_FORMAT"), "json", StringComparison.OrdinalIgnoreCase);

        var bootstrapConfig = new LoggerConfiguration();
        SerilogSetup.Configure(bootstrapConfig, builder.Configuration, useJsonLogs);

        // CreateLogger (not CreateBootstrapLogger): WebApplicationFactory builds multiple hosts in
        // one process; a bootstrap/reloadable logger freezes on the first host and throws
        // "The logger is already frozen" on the next. UseSerilog below replaces Log.Logger with a
        // fresh config from SerilogSetup, so Console / LOG_FORMAT stay identical.
        Log.Logger = bootstrapConfig.CreateLogger();

        // writeToProviders forwards events to MEL providers (including OTLP). Enable only when OTLP
        // log export is on; otherwise behavior matches a plain UseSerilog(). Clear default MEL
        // providers *before* UseSerilog so we do not strip SerilogLoggerProvider (needed for
        // ILogger<T> → Serilog → Console), while still avoiding duplicate stdout from the
        // framework Console logger when writeToProviders is on.
        var otlpLogExportEnabled = OpenTelemetrySetup.IsOtlpLogExportEnabled(builder.Configuration);
        if (otlpLogExportEnabled)
        {
            OpenTelemetrySetup.ClearDefaultLoggerProvidersForOtlp(builder);
        }

        builder.Host.UseSerilog(
            (context, configuration) => SerilogSetup.Configure(configuration, context.Configuration, useJsonLogs),
            writeToProviders: otlpLogExportEnabled);

        return builder;
    }
}
