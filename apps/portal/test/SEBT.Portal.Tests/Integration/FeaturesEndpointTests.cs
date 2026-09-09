using System.Net;
using System.Net.Http.Json;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class FeaturesEndpointTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FeaturesEndpointTests(PortalWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetFeatures_WithoutAuth_ReturnsPublicFlagsAndOmitsSensitiveFlags()
    {
        using var response = await _client.GetAsync("/api/features");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var flags = await response.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.NotNull(flags);

        Assert.False(flags.ContainsKey(FeatureFlags.BypassOtp));
        Assert.False(flags.ContainsKey(FeatureFlags.TestErrorEndpointsEnabled));
        Assert.True(flags.ContainsKey(FeatureFlags.OutagePageEnabled));
        Assert.True(flags.ContainsKey("enable_beta_banner"));
    }
}
