using System.Text.Json;
using SEBT.Portal.Api.Startup;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for the /health endpoint using the real HTTP pipeline.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class HealthCheckEndpointTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckEndpointTests(PortalWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
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

        // Overall may be Healthy (SQL reachable) or Degraded (SQL unreachable in CI).
        // Degraded still returns HTTP 200 so ALB keeps the task in service.
        var status = root.GetProperty("status").GetString();
        Assert.True(
            status is "Healthy" or "Degraded",
            $"Unexpected overall status: {status}");

        Assert.True(root.TryGetProperty("totalDuration", out var duration));
        Assert.Equal(JsonValueKind.Number, duration.ValueKind);
        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);

        // Portal DB check is always registered (state plugins may also appear depending on env).
        var checkNames = checks.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();
        Assert.Contains(PortalDbHealthCheckExtensions.CheckName, checkNames);
    }
}
