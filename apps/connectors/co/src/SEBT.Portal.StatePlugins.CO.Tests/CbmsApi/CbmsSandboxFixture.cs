using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Shared xUnit fixture that creates a <see cref="CbmsSebtApiClient"/> pointed at the
/// CBMS sandbox (UAT) environment. When no credentials are configured,
/// <see cref="CredentialsConfigured"/> is false and tests should skip gracefully.
/// When <c>Cbms:UseMockResponses</c> is true, uses mock responses from the API spec examples instead.
/// </summary>
/// <remarks>
/// Configure credentials via:
/// <list type="bullet">
///   <item>User secrets:
///     <c>dotnet user-secrets set "Cbms:ClientId" "&lt;id&gt;"</c> and
///     <c>dotnet user-secrets set "Cbms:ClientSecret" "&lt;secret&gt;"</c>
///   </item>
///   <item>Environment variables:
///     <c>Cbms__ClientId</c> and <c>Cbms__ClientSecret</c>
///   </item>
/// </list>
/// Enable mock responses (no real API calls): <c>Cbms:UseMockResponses=true</c> or <c>Cbms__UseMockResponses=true</c>.
/// API and token URLs default to <see cref="CbmsDefaults"/> UAT values;
/// optional overrides: <c>Cbms:ApiBaseUrl</c> / <c>Cbms:TokenEndpointUrl</c> (or env <c>Cbms__*</c>).
/// </remarks>
public class CbmsSandboxFixture : IAsyncLifetime
{
    /// <summary>The configured Kiota client, or null when no credentials or mocks are available.</summary>
    public CbmsSebtApiClient? Client { get; private set; }

    /// <summary>Whether tests can run (real sandbox credentials or mock responses configured).</summary>
    public bool CredentialsConfigured { get; private set; }

    /// <summary>Whether mock responses are being used instead of the real sandbox.</summary>
    public bool UseMockResponses { get; private set; }

    /// <summary>
    /// In-memory configuration for <see cref="SEBT.Portal.StatePlugins.CO.ColoradoAddressUpdateService"/> against UAT,
    /// or null when credentials are not configured (including mock-response mode).
    /// </summary>
    public IConfiguration? ColoradoCbmsConfiguration { get; private set; }

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CbmsSandboxFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var useMock = configuration["Cbms:UseMockResponses"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || configuration["Cbms__UseMockResponses"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        if (useMock)
        {
            CredentialsConfigured = true;
            UseMockResponses = true;

            var services = new ServiceCollection();
            services.AddHybridCache();
            var provider = services.BuildServiceProvider();
            var cache = provider.GetRequiredService<HybridCache>();
            var dataStore = new MockCbmsDataStore(cache);

            Client = CbmsSebtApiClientFactory.Create(
                clientId: "mock-client-id",
                clientSecret: "mock-client-secret",
                CbmsDefaults.SandboxApiBaseUrl,
                CbmsDefaults.SandboxTokenEndpointUrl,
                new MockCbmsHttpHandler(dataStore));
            return Task.CompletedTask;
        }

        var clientId = configuration["Cbms:ClientId"];
        var clientSecret = configuration["Cbms:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            CredentialsConfigured = false;
            return Task.CompletedTask;
        }

        CredentialsConfigured = true;
        var apiUrl = configuration["Cbms:ApiBaseUrl"] ?? CbmsDefaults.SandboxApiBaseUrl;
        var tokenUrl = configuration["Cbms:TokenEndpointUrl"] ?? CbmsDefaults.SandboxTokenEndpointUrl;
        Client = CbmsSebtApiClientFactory.Create(
            clientId,
            clientSecret,
            apiUrl,
            tokenUrl);

        ColoradoCbmsConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = clientId,
                ["Cbms:ClientSecret"] = clientSecret,
                ["Cbms:ApiBaseUrl"] = apiUrl,
                ["Cbms:TokenEndpointUrl"] = tokenUrl
            })
            .Build();

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("CbmsSandbox")]
public class CbmsSandboxCollection : ICollectionFixture<CbmsSandboxFixture>
{
}
