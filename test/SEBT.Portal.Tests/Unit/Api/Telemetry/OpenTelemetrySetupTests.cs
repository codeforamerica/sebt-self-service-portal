using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using SEBT.Portal.Core.AppSettings;

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
    public void UseLogExporter_DefaultsToConsole_SoOtlpIsOptIn()
    {
        // OTLP log export must be opt-in: with no configuration the CloudWatch/Console path
        // is left untouched, so a plain OtelSettings instance defaults the log exporter off.
        Assert.Equal(ExporterKind.Console, new OpenTelemetrySettings().UseLogExporter);
    }

    [Fact]
    public void UseLogExporter_BindsToOtlp_WhenConfigured()
    {
        // Mirrors the binding path in OpenTelemetrySetup.SetupOpenTelemetry: the "Otel" section
        // is bound onto OtelSettings, and the config binder maps the string case-insensitively
        // onto the ExporterKind enum.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenTelemetrySettings.SectionName}:{nameof(OpenTelemetrySettings.UseLogExporter)}"] = "otlp",
            })
            .Build();

        var settings = config.GetSection(OpenTelemetrySettings.SectionName).Get<OpenTelemetrySettings>();

        Assert.NotNull(settings);
        Assert.Equal(ExporterKind.Otlp, settings.UseLogExporter);
    }
}
