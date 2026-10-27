using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Reads CBMS configuration. Uses Cbms:ClientId, Cbms:ClientSecret, Cbms:ApiBaseUrl,
/// Cbms:TokenEndpointUrl (and Cbms__* env vars when configuration is null or keys are absent).
/// Mock responses come from the <c>use_mock_cbms_responses</c> feature flag.
/// Precedence: configuration source first, then environment variables. Uses <see cref="CbmsDefaults"/> when URLs are not set.
/// </summary>
internal static class CbmsOptionsHelper
{
    /// <summary>
    /// Configuration key for <see cref="CbmsConnectionOptions.UseMockResponses"/>.
    /// Matches <c>FeatureFlags.UseMockCbmsResponses</c> in the portal.
    /// </summary>
    internal const string UseMockResponsesConfigurationKey = "FeatureManagement:use_mock_cbms_responses";

    public static CbmsConnectionOptions GetCbmsOptions(IConfiguration? configuration, ILogger? logger = null)
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

        var useMockResponsesRaw = configuration?[UseMockResponsesConfigurationKey]
            ?? Environment.GetEnvironmentVariable("FeatureManagement__use_mock_cbms_responses");
        var useMockResponses = useMockResponsesRaw?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (useMockResponses && IsProduction(configuration))
        {
            logger?.LogWarning(
                "Feature flag {FeatureFlag} is enabled in Production and will be ignored.",
                "use_mock_cbms_responses");
            useMockResponses = false;
        }

        return new CbmsConnectionOptions(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl, useMockResponses);
    }

    private static bool IsProduction(IConfiguration? configuration)
    {
        // A missing name is Production, matching IHostEnvironment. A provided
        // configuration is not filled in from the process environment.
        var environmentName = configuration is not null
            ? configuration["environment"] ?? configuration["ASPNETCORE_ENVIRONMENT"]
            : Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.IsNullOrEmpty(environmentName)
            || string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
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
