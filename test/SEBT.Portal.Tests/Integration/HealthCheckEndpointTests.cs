using System.Text.Json;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for the /health endpoint using the real HTTP pipeline.
/// </summary>
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

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("totalDuration", out var duration));
        Assert.Equal(JsonValueKind.Number, duration.ValueKind);
        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);
        // No plugins loaded in test → no state health checks → empty array
        Assert.Equal(0, checks.GetArrayLength());
    }
}
