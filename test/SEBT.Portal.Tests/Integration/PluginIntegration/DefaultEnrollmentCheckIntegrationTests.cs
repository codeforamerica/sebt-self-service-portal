using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Api.Composition.Defaults;
using SEBT.Portal.Core.Services;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Integration tests that verify the DefaultEnrollmentCheckService fallback
/// when no state-specific plugin is loaded. Exercises the full HTTP pipeline:
/// POST /api/enrollment/check → controller → handler → DefaultEnrollmentCheckService.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class DefaultEnrollmentCheckIntegrationTests : IDisposable
{
    private const int MaxPluginPathIndices = 8;

    private readonly DefaultEnrollmentCheckWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DefaultEnrollmentCheckIntegrationTests()
    {
        _factory = new DefaultEnrollmentCheckWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostCheck_WithDefaultPlugin_ReturnsNonMatch()
    {
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

        var response = await _client.PostAsJsonAsync("/api/enrollment/check", requestBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Top-level message should indicate no service is configured
        Assert.Equal("No enrollment check service configured.",
            json.GetProperty("message").GetString());

        // Each child should get NonMatch status
        var results = json.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());

        var first = results[0];
        Assert.Equal("Jane", first.GetProperty("firstName").GetString());
        Assert.Equal("Doe", first.GetProperty("lastName").GetString());
        Assert.Equal("2015-03-12", first.GetProperty("dateOfBirth").GetString());
        Assert.Equal("NonMatch", first.GetProperty("status").GetString());
        Assert.Equal("Lincoln Elementary", first.GetProperty("schoolName").GetString());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// Loads no connector plugins and always registers <see cref="DefaultEnrollmentCheckService"/>.
    /// </summary>
    private sealed class DefaultEnrollmentCheckWebApplicationFactory : PluginIntegrationWebApplicationFactory
    {
        public DefaultEnrollmentCheckWebApplicationFactory()
            : base(pluginDir: null)
        {
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            var pluginsNonePath = PluginPathResolver.Resolve("plugins-none");
            Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", pluginsNonePath);
            for (var i = 1; i < MaxPluginPathIndices; i++)
            {
                Environment.SetEnvironmentVariable($"PluginAssemblyPaths__{i}", null);
            }

            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(IEnrollmentCheckSubmissionLogger))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddScoped(_ => Substitute.For<IEnrollmentCheckSubmissionLogger>());

                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(IEnrollmentCheckService))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IEnrollmentCheckService, DefaultEnrollmentCheckService>();
            });
        }
    }
}
