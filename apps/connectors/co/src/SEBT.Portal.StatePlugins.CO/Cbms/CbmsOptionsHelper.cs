using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Reads CBMS configuration. Uses Cbms:ClientId, Cbms:ClientSecret, Cbms:ApiBaseUrl,
/// Cbms:TokenEndpointUrl (and Cbms__* env vars when configuration is null or keys are absent).
/// Precedence: configuration source first, then environment variables. Uses <see cref="CbmsDefaults"/> when URLs are not set.
/// </summary>
internal static class CbmsOptionsHelper
{
    public static CbmsConnectionOptions GetCbmsOptions(IConfiguration? configuration)
    {
        var clientId = configuration?["Cbms:ClientId"]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientId")
            ?? string.Empty;
        var clientSecret = configuration?["Cbms:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret")
            ?? string.Empty;
        var apiBaseUrl = configuration?["Cbms:ApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl")
            ?? CbmsDefaults.SandboxApiBaseUrl;
        var tokenEndpointUrl = configuration?["Cbms:TokenEndpointUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl")
            ?? CbmsDefaults.SandboxTokenEndpointUrl;

        var useMockResponsesRaw = configuration?["Cbms:UseMockResponses"]
            ?? Environment.GetEnvironmentVariable("Cbms__UseMockResponses");
        var useMockResponses = useMockResponsesRaw?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        return new CbmsConnectionOptions(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl, useMockResponses);
    }
}

/// <summary>
/// Connection options for the CBMS SEBT API. Built from configuration and environment variables.
/// </summary>
public sealed record CbmsConnectionOptions(
    string ClientId,
    string ClientSecret,
    string ApiBaseUrl,
    string TokenEndpointUrl,
    bool UseMockResponses = false)
{
    /// <summary>
    /// True when real credentials are configured or mock responses are enabled.
    /// </summary>
    public bool IsConfigured =>
        UseMockResponses || (!string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret));
}
