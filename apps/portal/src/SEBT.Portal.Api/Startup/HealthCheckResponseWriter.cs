using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Writes structured JSON responses for the /health endpoint, including
/// overall status, total duration, and per-check details.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Writes a <see cref="HealthReport"/> as structured JSON to the HTTP response.
    /// </summary>
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsJsonAsync(result, JsonOptions);
    }
}
