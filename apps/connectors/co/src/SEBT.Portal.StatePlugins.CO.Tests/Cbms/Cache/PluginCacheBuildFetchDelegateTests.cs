using System.Net;
using System.Text;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

[Collection("PluginCache")]
public class PluginCacheBuildFetchDelegateTests
{
    [Fact]
    public async Task BuildFetchDelegate_sends_ebtCardService_Y_in_request_url()
    {
        string? capturedApiUrl = null;
        var handler = new UrlCapturingHttpHandler(url => capturedApiUrl = url);

        var client = CbmsSebtApiClientFactory.Create(
            "test-client-id",
            "test-client-secret",
            "https://api.example.com",
            "https://api.example.com/token",
            handler);

        var @delegate = PluginCache.BuildFetchDelegate(client);
        await @delegate("3035551234", true, CancellationToken.None);

        Assert.NotNull(capturedApiUrl);
        Assert.Contains("ebtCardService=Y", capturedApiUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildFetchDelegate_sends_ebtCardService_N_when_includeCardService_false()
    {
        string? capturedApiUrl = null;
        var handler = new UrlCapturingHttpHandler(url => capturedApiUrl = url);

        var client = CbmsSebtApiClientFactory.Create(
            "test-client-id",
            "test-client-secret",
            "https://api.example.com",
            "https://api.example.com/token",
            handler);

        var @delegate = PluginCache.BuildFetchDelegate(client);
        await @delegate("3035551234", false, CancellationToken.None);

        Assert.NotNull(capturedApiUrl);
        Assert.Contains("ebtCardService=N", capturedApiUrl, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UrlCapturingHttpHandler : HttpMessageHandler
    {
        private readonly Action<string> _captureUrl;

        public UrlCapturingHttpHandler(Action<string> captureUrl) => _captureUrl = captureUrl;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;

            if (url.Contains("token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            _captureUrl(url);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"stdntEnrollDtls":[]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
