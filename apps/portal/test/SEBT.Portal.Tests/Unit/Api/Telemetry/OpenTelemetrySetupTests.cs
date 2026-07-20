using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using SEBT.Portal.Api.Telemetry;
using SEBT.Portal.Core.AppSettings;
using Serilog;

namespace SEBT.Portal.Tests.Unit.Api.Telemetry;

/// <summary>
/// Verifies the OTLP log-export wiring in <see cref="OpenTelemetrySetup"/>: the exporter
/// is gated off by default (so the CloudWatch/Console path is never disrupted) and, when the
/// OTel logging pipeline is active, records carry the shared service resource and correlate
/// with the active trace/span.
/// </summary>
public class OpenTelemetrySetupTests
{
    [Fact]
    public void BuildLogsResourceBuilder_TagsRecordsWithServiceName()
    {
        var resource = OpenTelemetrySetup.BuildLogsResourceBuilder().Build();

        Assert.Contains(
            resource.Attributes,
            attribute => attribute.Key == "service.name"
                         && (string)attribute.Value == OpenTelemetrySetup.ServiceName);
    }

    [Fact]
    public void ConfigureLoggingCommon_EmitsFormattedRecordsToExporter()
    {
        var exported = new List<LogRecord>();

        var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                // Apply the exact production common config, then swap the OTLP exporter
                // for an in-memory exporter so we can assert on what would be shipped.
                OpenTelemetrySetup.ConfigureLoggingCommon(options);
                options.AddInMemoryExporter(exported);
            }));

        loggerFactory.CreateLogger("Test").LogInformation("hello otlp");
        loggerFactory.Dispose();

        var record = Assert.Single(exported);
        Assert.Equal("hello otlp", record.FormattedMessage);
    }

    [Fact]
    public void ConfigureLoggingCommon_CorrelatesRecordsWithActiveTraceSpan()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var exported = new List<LogRecord>();

        var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                OpenTelemetrySetup.ConfigureLoggingCommon(options);
                options.AddInMemoryExporter(exported);
            }));

        using (var activity = new Activity("test-operation").Start())
        {
            loggerFactory.CreateLogger("Test").LogInformation("correlated");

            var record = Assert.Single(exported);
            Assert.Equal(activity.TraceId, record.TraceId);
            Assert.Equal(activity.SpanId, record.SpanId);
        }

        loggerFactory.Dispose();
    }

    [Fact]
    public void IsOtlpLogExportEnabled_DefaultsToFalse_SoWriteToProvidersStaysOff()
    {
        // Program.cs passes this to UseSerilog(writeToProviders:). Opt-in default keeps
        // Serilog from fanning out to MEL providers when OTLP is off.
        Assert.False(OpenTelemetrySetup.IsOtlpLogExportEnabled(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void IsOtlpLogExportEnabled_IsTrue_WhenConfiguredOtlp()
    {
        var config = BuildOtelConfig(useLogExporter: "otlp");

        Assert.True(OpenTelemetrySetup.IsOtlpLogExportEnabled(config));
    }

    [Fact]
    public void IsOtlpLogExportEnabled_IsFalse_WhenConfiguredConsole()
    {
        var config = BuildOtelConfig(useLogExporter: "console");

        Assert.False(OpenTelemetrySetup.IsOtlpLogExportEnabled(config));
    }

    [Fact]
    public void SetupOpenTelemetry_WhenOtlpLogsOff_LeavesDefaultLoggerProviders()
    {
        var builder = CreateWebAppBuilder(useLogExporter: "console");
        var before = LoggerProviderTypeNames(builder);

        builder.SetupOpenTelemetry();

        var after = LoggerProviderTypeNames(builder);
        Assert.Contains(after, name => name.Contains("Console", StringComparison.Ordinal));
        Assert.DoesNotContain(after, name => name.Contains("OpenTelemetry", StringComparison.Ordinal));
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public void SetupOpenTelemetry_WhenOtlpLogsOn_ClearsDefaultsAndRegistersOtelLogger()
    {
        var builder = CreateWebAppBuilder(useLogExporter: "otlp");

        builder.SetupOpenTelemetry();

        var after = LoggerProviderTypeNames(builder);
        Assert.DoesNotContain(after, name => name.Contains("ConsoleLoggerProvider", StringComparison.Ordinal));
        Assert.Contains(after, name => name.Contains("OpenTelemetry", StringComparison.Ordinal));
    }

    private static IConfiguration BuildOtelConfig(string useLogExporter) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenTelemetrySettings.SectionName}:{nameof(OpenTelemetrySettings.UseLogExporter)}"] =
                    useLogExporter,
            })
            .Build();

    private static WebApplicationBuilder CreateWebAppBuilder(string useLogExporter)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{OpenTelemetrySettings.SectionName}:{nameof(OpenTelemetrySettings.UseLogExporter)}"] =
                useLogExporter,
            // Keep traces/metrics on console so tests do not require an OTLP endpoint.
            [$"{OpenTelemetrySettings.SectionName}:{nameof(OpenTelemetrySettings.UseTracingExporter)}"] =
                "console",
            [$"{OpenTelemetrySettings.SectionName}:{nameof(OpenTelemetrySettings.UseMetricsExporter)}"] =
                "console",
        });
        return builder;
    }

    private static List<string> LoggerProviderTypeNames(WebApplicationBuilder builder) =>
        builder.Services
            .Where(descriptor => descriptor.ServiceType == typeof(ILoggerProvider))
            .Select(descriptor =>
                descriptor.ImplementationType?.Name
                ?? descriptor.ImplementationInstance?.GetType().Name
                ?? descriptor.ImplementationFactory?.Method.ReturnType.Name
                ?? "unknown")
            .ToList();
}

/// <summary>
/// Verifies Serilog Console formatting used for the stdout → CloudWatch path.
/// </summary>
public class SerilogSetupTests
{
    [Fact]
    public void Configure_WithJsonLogs_WritesDatadogShapedConsoleLine()
    {
        var output = CaptureConsoleOutput(() =>
        {
            var configuration = new LoggerConfiguration();
            SerilogSetup.Configure(configuration, new ConfigurationBuilder().Build(), useJsonLogs: true);
            using var log = configuration.CreateLogger();
            log.Information("cloudwatch-probe");
        });

        Assert.Contains("cloudwatch-probe", output, StringComparison.Ordinal);
        Assert.Contains("\"status\"", output, StringComparison.Ordinal);
        Assert.Contains("\"message\"", output, StringComparison.Ordinal);
        Assert.Contains("\"date\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_WithTextLogs_WritesHumanReadableConsoleLine()
    {
        var output = CaptureConsoleOutput(() =>
        {
            var configuration = new LoggerConfiguration();
            SerilogSetup.Configure(configuration, new ConfigurationBuilder().Build(), useJsonLogs: false);
            using var log = configuration.CreateLogger();
            log.Information("local-dev-probe");
        });

        Assert.Contains("local-dev-probe", output, StringComparison.Ordinal);
        Assert.Contains("INF", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\"", output, StringComparison.Ordinal);
    }

    private static string CaptureConsoleOutput(Action act)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            act();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
