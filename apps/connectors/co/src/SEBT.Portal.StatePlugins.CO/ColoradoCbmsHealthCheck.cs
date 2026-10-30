using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// CBMS ping check that re-reads connection options on every run so
/// <c>use_mock_cbms_responses</c> can flip without a restart.
/// </summary>
internal sealed class ColoradoCbmsHealthCheck(
    IConfiguration? configuration,
    ILogger? logger = null) : IHealthCheck
{
    internal const string MissingCredentialsMessage =
        "CBMS credentials are not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret in appsettings or Cbms__ClientId and Cbms__ClientSecret environment variables. " +
        "Or enable the use_mock_cbms_responses feature flag for local development with mock responses.";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = CbmsOptionsHelper.GetCbmsOptions(configuration, logger);
        if (!options.IsConfigured)
        {
            return HealthCheckResult.Degraded(MissingCredentialsMessage);
        }

        HttpMessageHandler? handler = null;
        var clientId = options.UseMockResponses ? "mock-client-id" : options.ClientId;
        var clientSecret = options.UseMockResponses ? "mock-client-secret" : options.ClientSecret;

        if (options.UseMockResponses)
        {
            handler = new MockCbmsHttpHandler();
        }

        var ping = new CbmsApiHealthCheck(
            clientId, clientSecret, options.ApiBaseUrl, options.TokenEndpointUrl, handler);
        return await ping.CheckHealthAsync(context, cancellationToken);
    }
}
