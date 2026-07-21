using Serilog;
using Serilog.Templates;

namespace SEBT.Portal.Api.Telemetry;

/// <summary>
/// Shared Serilog wiring for the bootstrap logger and the host logger so Console /
/// LOG_FORMAT stay identical in both.
/// </summary>
internal static class SerilogSetup
{
    internal static void Configure(
        LoggerConfiguration configuration,
        IConfiguration appConfiguration,
        bool useJsonLogs)
    {
        configuration
            .ReadFrom.Configuration(appConfiguration)
            .Enrich.FromLogContext()
            .Enrich.WithOtelTracingSpanId()
            .Enrich.WithPortalUserInfo();

        if (useJsonLogs)
        {
            configuration.WriteTo.Console(new ExpressionTemplate(
                "{ {date: @t, timestamp: @t, status: @l, level: @l, message: @m, exception: @x, ..@p} }\n"));
        }
        else
        {
            configuration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
    }
}
