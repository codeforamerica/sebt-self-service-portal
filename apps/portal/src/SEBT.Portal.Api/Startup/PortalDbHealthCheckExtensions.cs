using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Registers the portal database connectivity health check.
/// </summary>
internal static class PortalDbHealthCheckExtensions
{
    internal const string CheckName = "portal-db";

    public static IServiceCollection AddPortalDbHealthCheck(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddHealthChecks().AddCheck(
            CheckName,
            new PortalDbHealthCheck(connectionString),
            failureStatus: HealthStatus.Degraded,
            tags: ["database", "portal"]);

        return services;
    }
}
