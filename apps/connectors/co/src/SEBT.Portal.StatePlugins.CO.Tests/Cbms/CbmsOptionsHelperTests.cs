using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsOptionsHelperTests
{
    [Fact]
    public void GetCbmsOptions_empty_config_returns_IsConfigured_false()
    {
        // Blank credentials explicitly: CI (and local dev) may set Cbms__* env vars; helper falls back to those.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:UseMockResponses"] = "false",
                ["Cbms:ClientId"] = "",
                ["Cbms:ClientSecret"] = ""
            })
            .Build();
        var options = CbmsOptionsHelper.GetCbmsOptions(config);

        Assert.False(options.IsConfigured);
        Assert.Empty(options.ClientId);
        Assert.Empty(options.ClientSecret);
    }

    [Fact]
    public void GetCbmsOptions_with_client_id_and_secret_returns_IsConfigured_true()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "my-client-id",
                ["Cbms:ClientSecret"] = "my-client-secret"
            })
            .Build();
        var options = CbmsOptionsHelper.GetCbmsOptions(config);

        Assert.True(options.IsConfigured);
        Assert.Equal("my-client-id", options.ClientId);
        Assert.Equal("my-client-secret", options.ClientSecret);
    }

    [Fact]
    public void GetCbmsOptions_uses_CbmsDefaults_when_urls_not_set()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "id",
                ["Cbms:ClientSecret"] = "secret"
            })
            .Build();
        var options = CbmsOptionsHelper.GetCbmsOptions(config);

        Assert.Equal(CbmsDefaults.SandboxApiBaseUrl, options.ApiBaseUrl);
        Assert.Equal(CbmsDefaults.SandboxTokenEndpointUrl, options.TokenEndpointUrl);
    }

    [Fact]
    public void GetCbmsOptions_UseMockResponses_true_returns_IsConfigured_true()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:UseMockResponses"] = "true"
            })
            .Build();
        var options = CbmsOptionsHelper.GetCbmsOptions(config);

        Assert.True(options.IsConfigured);
        Assert.True(options.UseMockResponses);
    }

    [Fact]
    public void GetCbmsOptions_uses_config_urls_when_set()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "id",
                ["Cbms:ClientSecret"] = "secret",
                ["Cbms:ApiBaseUrl"] = "https://custom-api.example.com",
                ["Cbms:TokenEndpointUrl"] = "https://custom-token.example.com/token"
            })
            .Build();
        var options = CbmsOptionsHelper.GetCbmsOptions(config);

        Assert.Equal("https://custom-api.example.com", options.ApiBaseUrl);
        Assert.Equal("https://custom-token.example.com/token", options.TokenEndpointUrl);
    }
}
