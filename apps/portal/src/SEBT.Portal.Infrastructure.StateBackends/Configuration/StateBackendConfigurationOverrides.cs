using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;

namespace SEBT.Portal.Infrastructure.StateBackends.Configuration;

/// <summary>
/// Applies the URL overrides a loaded config names (<c>baseUrlRef</c>, <c>tokenUrlRef</c>), so the
/// adapter only ever sees resolved absolute URLs. Runs at startup, so a missing URL fails loud there.
/// </summary>
internal static class StateBackendConfigurationOverrides
{
    public static StateBackendConfiguration Apply(
        StateBackendConfiguration configuration, IConfiguration appConfiguration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(appConfiguration);

        Uri baseUrl = EnsureTrailingSlash(ResolveUrl(
            configuration.BaseUrl, configuration.BaseUrlRef, appConfiguration, "baseUrl"));

        StateBackendAuthScheme auth = configuration.Auth is StateBackendOAuthClientCredentialsAuthScheme oauth
            ? oauth with { TokenUrl = ResolveUrl(oauth.TokenUrl, oauth.TokenUrlRef, appConfiguration, "tokenUrl") }
            : configuration.Auth;

        return configuration with { BaseUrl = baseUrl, Auth = auth };
    }

    private static Uri ResolveUrl(
        Uri? defaultUrl, string? urlRef, IConfiguration appConfiguration, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(urlRef))
        {
            string? configuredValue = appConfiguration[urlRef];
            if (!string.IsNullOrWhiteSpace(configuredValue))
            {
                if (!Uri.TryCreate(configuredValue, UriKind.Absolute, out Uri? configuredUrl))
                {
                    throw new InvalidOperationException(
                        $"Configuration key '{urlRef}' (named by '{fieldName}Ref') is not an absolute URL.");
                }

                return configuredUrl;
            }
        }

        return defaultUrl
            ?? throw new InvalidOperationException(
                $"Configuration key '{urlRef}' (named by '{fieldName}Ref') has no value, " +
                $"and the config sets no '{fieldName}' default.");
    }

    // Operation paths are relative, so without a trailing slash the base URL's last segment is dropped.
    private static Uri EnsureTrailingSlash(Uri url) =>
        url.AbsolutePath.EndsWith('/')
            ? url
            : new UriBuilder(url) { Path = url.AbsolutePath + "/" }.Uri;
}
