using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Verifies connectivity to the portal SQL Server database by opening a
/// connection and executing <c>SELECT 1</c>.
/// </summary>
internal class PortalDbHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Degraded (not Unhealthy) so ALB continues routing to the task while
            // /health still surfaces the DB outage in its structured response.
            return HealthCheckResult.Degraded(ex.Message, ex);
        }
    }
}
