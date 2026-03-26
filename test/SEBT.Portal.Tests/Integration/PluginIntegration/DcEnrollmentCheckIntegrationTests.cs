using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Integration tests that load the real DC connector plugin (DcEnrollmentCheckService)
/// via MEF and exercise the full HTTP pipeline: POST /api/enrollment/check → controller →
/// use case handler → DcEnrollmentCheckService → stub stored procedure → response.
///
/// These tests require:
/// - DC plugin DLLs built into plugins-dc/ (with all transitive dependencies)
/// - Docker running (for the MSSQL Testcontainer)
///
/// Tests skip gracefully when plugin DLLs are not present or can't be loaded.
/// </summary>
[Collection("Integration")]
public class DcEnrollmentCheckIntegrationTests : IClassFixture<DcSourceDatabaseFixture>, IDisposable
{
    private readonly PluginIntegrationWebApplicationFactory? _factory;
    private readonly HttpClient? _client;
    private readonly bool _canRun;
    private readonly string _skipReason;

    public DcEnrollmentCheckIntegrationTests(DcSourceDatabaseFixture dcDatabase)
    {
        if (!PluginPathResolver.HasPluginDlls("plugins-dc"))
        {
            _canRun = false;
            _skipReason = "DC plugin DLLs not found in plugins-dc/";
            return;
        }

        // Plugin DLLs exist, but they may fail to load if transitive dependencies
        // (e.g., Microsoft.Kiota.Abstractions) aren't in the directory. Catch and skip.
        try
        {
            _factory = new PluginIntegrationWebApplicationFactory(
                pluginDir: "plugins-dc",
                configOverrides: new Dictionary<string, string>
                {
                    ["DCConnector:ConnectionString"] = dcDatabase.ConnectionString
                });
            _client = _factory.CreateClient();
            _canRun = true;
            _skipReason = string.Empty;
        }
        catch (Exception ex)
        {
            _canRun = false;
            _skipReason = $"DC plugin DLLs could not be loaded: {ex.GetBaseException().Message}";
        }
    }

    [SkippableFact]
    public async Task PostCheck_WithDcPlugin_EligibleChild_ReturnsMatch()
    {
        Skip.IfNot(_canRun, _skipReason);

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Jane",
                    lastName = "Doe",
                    dateOfBirth = "2015-03-12",
                    schoolName = "Lincoln Elementary"
                }
            }
        };

        var response = await _client!.PostAsJsonAsync("/api/enrollment/check", requestBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var results = json.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());

        var first = results[0];
        Assert.Equal("Jane", first.GetProperty("firstName").GetString());
        Assert.Equal("Doe", first.GetProperty("lastName").GetString());
        Assert.Equal("2015-03-12", first.GetProperty("dateOfBirth").GetString());
        Assert.Equal("Match", first.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task PostCheck_WithDcPlugin_IneligibleChild_ReturnsNonMatch()
    {
        Skip.IfNot(_canRun, _skipReason);

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Nonexistent",
                    lastName = "Child",
                    dateOfBirth = "2016-01-01",
                    schoolName = "Unknown School"
                }
            }
        };

        var response = await _client!.PostAsJsonAsync("/api/enrollment/check", requestBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var results = json.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());

        var first = results[0];
        Assert.Equal("NonMatch", first.GetProperty("status").GetString());
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}
