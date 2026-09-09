using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Health check that verifies connectivity to the CBMS SEBT API
/// by authenticating and calling the ping endpoint.
/// </summary>
internal class CbmsApiHealthCheck(
    string clientId,
    string clientSecret,
    string apiBaseUrl,
    string tokenEndpointUrl,
    HttpMessageHandler? httpMessageHandler = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CbmsSebtApiClientFactory.Create(
                clientId, clientSecret, apiBaseUrl, tokenEndpointUrl, httpMessageHandler);
            await client.Ping.GetAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(ex.Message, ex);
        }
    }
}
