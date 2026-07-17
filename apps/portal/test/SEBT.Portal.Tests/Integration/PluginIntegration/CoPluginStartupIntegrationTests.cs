using System.Net;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Verifies that the application starts successfully with the CO plugin loaded.
/// This exercises the full MEF composition pipeline (including ILoggerFactory export)
/// without requiring CBMS API credentials. If MEF composition fails (e.g., a required
/// export is missing), WebApplicationFactory.CreateClient() will throw.
/// </summary>
[Collection("Integration")]
public class CoPluginStartupIntegrationTests : IDisposable
{
    private readonly PluginIntegrationWebApplicationFactory? _factory;
    private readonly HttpClient? _client;
    private readonly bool _canRun;
    private readonly string _skipReason;

    public CoPluginStartupIntegrationTests()
    {
        var pluginsAvailable = PluginPathResolver.HasPluginDlls("plugins-co");

        if (!pluginsAvailable)
        {
            _canRun = false;
            _skipReason = "CO plugin DLLs not found in plugins-co/";
        }
        else
        {
            _canRun = true;
            _skipReason = string.Empty;

            _factory = new PluginIntegrationWebApplicationFactory(
                pluginDir: "plugins-co",
                configOverrides: new Dictionary<string, string>
                {
                    // Provide dummy values so configuration validation passes.
                    // These are never called — we only hit /health.
                    ["COConnector:CbmsApiBaseUrl"] = "https://localhost:9999",
                    ["COConnector:CbmsApiKey"] = "test-key-not-used"
                });
            _client = _factory.CreateClient();
        }
    }

    [SkippableFact]
    public async Task Startup_WithCoPlugin_Succeeds()
    {
        Skip.IfNot(_canRun, _skipReason);

        // If MEF composition failed, CreateClient() in the constructor would have thrown.
        // Hitting /health confirms the app is fully started and serving requests.
        var response = await _client!.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}
