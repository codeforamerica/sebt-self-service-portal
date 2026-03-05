using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Integration tests that load the real CO connector plugin (ColoradoEnrollmentCheckService)
/// via MEF and exercise the full HTTP pipeline against the CBMS test API.
///
/// These tests require:
/// - CO plugin DLLs built into plugins-co/
/// - COConnector__CbmsApiBaseUrl and COConnector__CbmsApiKey environment variables set
///
/// Tests skip gracefully when either condition is not met.
/// </summary>
[Collection("PluginIntegration")]
public class CoEnrollmentCheckIntegrationTests : IDisposable
{
    private readonly PluginIntegrationWebApplicationFactory? _factory;
    private readonly HttpClient? _client;
    private readonly bool _canRun;
    private readonly string _skipReason;

    public CoEnrollmentCheckIntegrationTests()
    {
        var pluginsAvailable = PluginPathResolver.HasPluginDlls("plugins-co");
        var apiBaseUrl = Environment.GetEnvironmentVariable("COConnector__CbmsApiBaseUrl");
        var apiKey = Environment.GetEnvironmentVariable("COConnector__CbmsApiKey");

        if (!pluginsAvailable)
        {
            _canRun = false;
            _skipReason = "CO plugin DLLs not found in plugins-co/";
        }
        else if (string.IsNullOrEmpty(apiBaseUrl) || string.IsNullOrEmpty(apiKey))
        {
            _canRun = false;
            _skipReason = "COConnector__CbmsApiBaseUrl and/or COConnector__CbmsApiKey not set";
        }
        else
        {
            _canRun = true;
            _skipReason = string.Empty;

            _factory = new PluginIntegrationWebApplicationFactory(
                pluginDir: "plugins-co",
                configOverrides: new Dictionary<string, string>
                {
                    ["COConnector__CbmsApiBaseUrl"] = apiBaseUrl,
                    ["COConnector__CbmsApiKey"] = apiKey
                });
            _client = _factory.CreateClient();
        }
    }

    [SkippableFact]
    public async Task PostCheck_WithCoPlugin_ReturnsResponse()
    {
        Skip.IfNot(_canRun, _skipReason);

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Test",
                    lastName = "Student",
                    dateOfBirth = "2015-01-01",
                    schoolCode = "0001"
                }
            }
        };

        var response = await _client!.PostAsJsonAsync("/api/enrollment/check", requestBody);

        // The CBMS test API should return a valid response (200)
        // regardless of match/non-match status
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("results", out var results));
        Assert.True(results.GetArrayLength() > 0);

        // Verify the response has expected structure — we don't assert specific
        // match/non-match results since the external test API's data is outside
        // our control
        var first = results[0];
        Assert.True(first.TryGetProperty("status", out var status));
        var statusValue = status.GetString();
        Assert.Contains(statusValue, new[] { "Match", "PossibleMatch", "NonMatch", "Error" });
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}
