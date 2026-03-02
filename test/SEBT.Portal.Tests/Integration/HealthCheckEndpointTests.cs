using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for the /health endpoint using the real HTTP pipeline.
/// Uses WebApplicationFactory to spin up the application and make actual HTTP requests.
/// </summary>
public class HealthCheckEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;

    public HealthCheckEndpointTests(WebApplicationFactory<Program> factory)
    {
        // Override plugin assembly paths via environment variables BEFORE the server starts.
        // WebApplicationFactory lazily starts the server on CreateClient(), so env vars set here
        // are visible when Program.cs reads builder.Configuration during startup.
        // This prevents loading plugin DLLs (copied to test output by the API csproj)
        // that have unresolvable transitive dependencies in the test environment.
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace database services with no-op mocks so startup
                // doesn't require a real SQL Server instance.
                ReplaceWithMock<IDatabaseMigrator>(services);
                ReplaceWithMock<IDatabaseSeeder>(services);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithStructuredJson()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert - HTTP 200
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // Assert - Content-Type is JSON
        Assert.Equal("application/json",
            response.Content.Headers.ContentType?.MediaType);

        // Assert - Body contains structured health check data
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("totalDuration", out var duration));
        Assert.Equal(JsonValueKind.Number, duration.ValueKind);
        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);
        // No plugins loaded in test → no state health checks → empty array
        Assert.Equal(0, checks.GetArrayLength());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", null);
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", null);
    }

    /// <summary>
    /// Replaces an existing service registration with a no-op NSubstitute mock.
    /// </summary>
    private static void ReplaceWithMock<TService>(IServiceCollection services) where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddScoped(_ => Substitute.For<TService>());
    }
}
