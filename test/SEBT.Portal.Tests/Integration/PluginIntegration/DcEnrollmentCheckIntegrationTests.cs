using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.Services;
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
public class DcEnrollmentCheckIntegrationTests : IClassFixture<DcSourceDatabaseFixture>, IDisposable
{
    private readonly DcEnrollmentCheckWebApplicationFactory? _factory;
    private readonly HttpClient? _client;
    private readonly bool _canRun;
    private readonly string _skipReason;

    public DcEnrollmentCheckIntegrationTests(DcSourceDatabaseFixture dcDatabase)
    {
        DcEnrollmentCheckWebApplicationFactory? factory = null;
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
                factory = new DcEnrollmentCheckWebApplicationFactory(dcDatabase.ConnectionString);

                using (var scope = factory.Services.CreateScope())
                {
                    var enrollment = scope.ServiceProvider.GetRequiredService<IEnrollmentCheckService>();
                    if (enrollment.GetType().Name != "DcEnrollmentCheckService")
                    {
                        factory.Dispose();
                        factory = null;
                        skipReason =
                            "Expected DcEnrollmentCheckService but got " +
                            $"{enrollment.GetType().FullName}. Rebuild dc-connector and copy DLLs to plugins-dc.";
                    }
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

    private sealed class DcEnrollmentCheckWebApplicationFactory : PluginIntegrationWebApplicationFactory
    {
        private const int MaxPluginPathIndices = 8;
        private readonly string _connectionString;

        public DcEnrollmentCheckWebApplicationFactory(string connectionString)
            : base(pluginDir: "plugins-dc")
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", PluginPathResolver.Resolve("plugins-dc"));
            for (var i = 1; i < MaxPluginPathIndices; i++)
            {
                Environment.SetEnvironmentVariable($"PluginAssemblyPaths__{i}", null);
            }

            Environment.SetEnvironmentVariable("DCConnector__ConnectionString", _connectionString);
            Environment.SetEnvironmentVariable("DCConnector__CheckEligibilityProcName", "dbo.sp_CheckEligibility");

            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(IEnrollmentCheckSubmissionLogger))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddScoped(_ => Substitute.For<IEnrollmentCheckSubmissionLogger>());
            });
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("DCConnector__ConnectionString", null);
            Environment.SetEnvironmentVariable("DCConnector__CheckEligibilityProcName", null);
            base.Dispose(disposing);
        }
    }
}
