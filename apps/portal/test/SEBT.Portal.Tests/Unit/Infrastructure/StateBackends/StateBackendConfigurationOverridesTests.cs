using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class StateBackendConfigurationOverridesTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static StateBackendConfiguration BuildClientCredentialsConfig(
        Uri? baseUrl, string? baseUrlRef, Uri? tokenUrl, string? tokenUrlRef) =>
        StateBackendTestConfig.Base() with
        {
            BaseUrl = baseUrl,
            BaseUrlRef = baseUrlRef,
            Auth = new StateBackendOAuthClientCredentialsAuthScheme
            {
                TokenUrl = tokenUrl,
                TokenUrlRef = tokenUrlRef,
                ClientId = "co-client",
                ClientSecretRef = "Backend:ClientSecret",
            },
        };

    [Fact]
    public void Apply_UsesConfiguredUrls_WhenRefsResolve()
    {
        StateBackendConfiguration config = BuildClientCredentialsConfig(
            new Uri("https://uat.test/api/"), "Backend:ApiBaseUrl",
            new Uri("https://uat.test/token"), "Backend:TokenEndpointUrl");
        IConfiguration configuration = BuildConfiguration(new()
        {
            ["Backend:ApiBaseUrl"] = "https://prod.test/api/",
            ["Backend:TokenEndpointUrl"] = "https://prod.test/token",
        });

        StateBackendConfiguration resolved = StateBackendConfigurationOverrides.Apply(config, configuration);

        Assert.Equal(new Uri("https://prod.test/api/"), resolved.BaseUrl);
        var oauth = Assert.IsType<StateBackendOAuthClientCredentialsAuthScheme>(resolved.Auth);
        Assert.Equal(new Uri("https://prod.test/token"), oauth.TokenUrl);
    }

    [Fact]
    public void Apply_KeepsLiteralUrls_WhenRefsAreUnset()
    {
        StateBackendConfiguration config = BuildClientCredentialsConfig(
            new Uri("https://uat.test/api/"), "Backend:ApiBaseUrl",
            new Uri("https://uat.test/token"), "Backend:TokenEndpointUrl");

        StateBackendConfiguration resolved = StateBackendConfigurationOverrides.Apply(
            config, BuildConfiguration(new()));

        Assert.Equal(new Uri("https://uat.test/api/"), resolved.BaseUrl);
        var oauth = Assert.IsType<StateBackendOAuthClientCredentialsAuthScheme>(resolved.Auth);
        Assert.Equal(new Uri("https://uat.test/token"), oauth.TokenUrl);
    }

    // Config paths are relative, so a base URL without a trailing slash would drop its last segment.
    [Fact]
    public void Apply_AppendsTrailingSlash_ToConfiguredBaseUrl()
    {
        StateBackendConfiguration config = BuildClientCredentialsConfig(
            new Uri("https://uat.test/api/"), "Backend:ApiBaseUrl",
            new Uri("https://uat.test/token"), null);
        IConfiguration configuration = BuildConfiguration(new()
        {
            ["Backend:ApiBaseUrl"] = "https://prod.test/api",
        });

        StateBackendConfiguration resolved = StateBackendConfigurationOverrides.Apply(config, configuration);

        Assert.Equal(new Uri("https://prod.test/api/"), resolved.BaseUrl);
    }

    [Fact]
    public void Apply_Throws_WhenRefOnlyUrlIsMissingFromConfiguration()
    {
        StateBackendConfiguration config = BuildClientCredentialsConfig(
            null, "Backend:ApiBaseUrl",
            new Uri("https://uat.test/token"), null);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationOverrides.Apply(config, BuildConfiguration(new())));
        Assert.Contains("Backend:ApiBaseUrl", ex.Message);
    }

    [Fact]
    public void Apply_Throws_WhenConfiguredUrlIsNotAbsolute()
    {
        StateBackendConfiguration config = BuildClientCredentialsConfig(
            new Uri("https://uat.test/api/"), null,
            new Uri("https://uat.test/token"), "Backend:TokenEndpointUrl");
        IConfiguration configuration = BuildConfiguration(new()
        {
            ["Backend:TokenEndpointUrl"] = "not a url",
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationOverrides.Apply(config, configuration));
        Assert.Contains("Backend:TokenEndpointUrl", ex.Message);
    }

    [Fact]
    public void Apply_LeavesApiKeyAuthUnchanged()
    {
        StateBackendConfiguration config = StateBackendTestConfig.Base();

        StateBackendConfiguration resolved = StateBackendConfigurationOverrides.Apply(
            config, BuildConfiguration(new()));

        Assert.Equal(config.Auth, resolved.Auth);
        Assert.Equal(new Uri("http://backend.test/"), resolved.BaseUrl);
    }
}
