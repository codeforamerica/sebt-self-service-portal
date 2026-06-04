using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Integration tests that load the real DC connector plugin (DcEnrollmentCheckService)
/// via MEF and exercise the full HTTP pipeline: POST /api/enrollment/check → controller →
/// use case handler → DcEnrollmentCheckService → stub stored procedure → response.
///
/// These tests require:
/// - DC plugin DLLs built into plugins-dc/ (with all transitive dependencies)
/// - Docker running (for the MSSQL Testcontainer)
/// - <c>DCConnector:CheckEligibilityProcName</c> pointing at the fixture stub (<c>dbo.sp_CheckEligibility</c>),
///   matching production behavior where the proc name has no default.
///
/// Tests skip gracefully when plugin DLLs are not present or can't be loaded.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlServer")]
public class DcEnrollmentCheckIntegrationTests : IClassFixture<DcSourceDatabaseFixture>, IDisposable
{
    private readonly PluginIntegrationWebApplicationFactory? _factory;
    private readonly HttpClient? _client;
    private readonly bool _canRun;
    private readonly string _skipReason;

    public DcEnrollmentCheckIntegrationTests(DcSourceDatabaseFixture dcDatabase)
    {
        PluginIntegrationWebApplicationFactory? factory = null;
        HttpClient? client = null;
        var canRun = false;
        var skipReason = string.Empty;

        if (!PluginPathResolver.HasPluginDlls("plugins-dc"))
        {
            skipReason = "DC plugin DLLs not found in plugins-dc/";
        }
        else
        {
            try
            {
                factory = new PluginIntegrationWebApplicationFactory(
                    pluginDir: "plugins-dc",
                    configOverrides: new Dictionary<string, string>
                    {
                        ["DCConnector:ConnectionString"] = dcDatabase.ConnectionString,
                        // Required by DcEnrollmentCheckService (no default); must match dbo.sp_CheckEligibility in DcSourceDatabaseFixture.
                        ["DCConnector:CheckEligibilityProcName"] = "dbo.sp_CheckEligibility"
                    });

                using (var scope = factory.Services.CreateScope())
                {
                    var enrollment = scope.ServiceProvider.GetRequiredService<IEnrollmentCheckService>();
                    if (enrollment.GetType().Name != "DcEnrollmentCheckService")
                    {
                        factory.Dispose();
                        factory = null;
                        skipReason =
                            "Expected DcEnrollmentCheckService but got " +
                            $"{enrollment.GetType().FullName}. Rebuild dc-connector, copy DLLs to plugins-dc, " +
                            "and ensure no other enrollment plugin is loaded.";
                    }
                }

                if (factory != null && !CanConnectToDcSource(dcDatabase.ConnectionString, out var connectError))
                {
                    factory.Dispose();
                    factory = null;
                    skipReason = connectError;
                }

                if (factory != null)
                {
                    client = factory.CreateClient();
                    canRun = true;
                }
            }
            catch (Exception ex)
            {
                factory?.Dispose();
                factory = null;
                client?.Dispose();
                client = null;
                skipReason = $"DC plugin DLLs could not be loaded: {ex.GetBaseException().Message}";
            }
        }

        _factory = factory;
        _client = client;
        _canRun = canRun;
        _skipReason = skipReason;
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

    private static bool CanConnectToDcSource(string connectionString, out string skipReason)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            skipReason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            skipReason =
                $"DC source database is not reachable (Docker/Testcontainers may be unavailable): {ex.GetBaseException().Message}";
            return false;
        }
    }
}
