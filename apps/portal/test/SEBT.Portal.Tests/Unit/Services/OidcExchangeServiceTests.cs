using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class OidcExchangeServiceTests
{
    [Theory]
    [InlineData("http://localhost:8180/realms/sebt/.well-known/openid-configuration", false)]
    [InlineData("HTTP://localhost:8180/realms/sebt/.well-known/openid-configuration", false)]
    [InlineData("https://id.mycolorado.gov/.well-known/openid-configuration", true)]
    [InlineData("HTTPS://id.mycolorado.gov/.well-known/openid-configuration", true)]
    public void DiscoveryRequiresHttps_follows_configured_discovery_scheme(string url, bool expected)
    {
        Assert.Equal(expected, OidcExchangeService.DiscoveryRequiresHttps(url));
    }

    [Fact]
    public async Task GetDiscoveryConfigAsync_allows_http_discovery_endpoint()
    {
        await using var idp = await LocalOidcDiscoveryServer.StartAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:DiscoveryEndpoint"] = idp.DiscoveryUrl
            })
            .Build();

        var service = new OidcExchangeService(
            config,
            Substitute.For<IHttpClientFactory>(),
            NullLogger<OidcExchangeService>.Instance,
            Substitute.For<IOidcCallbackFailureLogger>());

        var oidcConfig = await service.GetDiscoveryConfigAsync(isStepUp: false);

        Assert.Equal(idp.Issuer, oidcConfig.Issuer);
        Assert.Equal(idp.AuthorizationEndpoint, oidcConfig.AuthorizationEndpoint);
    }

    [Fact]
    public async Task GetDiscoveryConfigAsync_throws_when_discovery_endpoint_missing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = new OidcExchangeService(
            config,
            Substitute.For<IHttpClientFactory>(),
            NullLogger<OidcExchangeService>.Instance,
            Substitute.For<IOidcCallbackFailureLogger>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetDiscoveryConfigAsync(isStepUp: false));
    }

    /// <summary>
    /// Minimal HTTP OIDC discovery + JWKS server for local IdP stand-in tests.
    /// </summary>
    private sealed class LocalOidcDiscoveryServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        private LocalOidcDiscoveryServer(HttpListener listener, string baseUrl)
        {
            _listener = listener;
            Issuer = $"{baseUrl}/realms/sebt";
            AuthorizationEndpoint = $"{baseUrl}/auth";
            DiscoveryUrl = $"{Issuer}/.well-known/openid-configuration";
            _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public string Issuer { get; }
        public string AuthorizationEndpoint { get; }
        public string DiscoveryUrl { get; }

        public static async Task<LocalOidcDiscoveryServer> StartAsync()
        {
            // Bind an ephemeral port; retry a few times if the chosen port races.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var port = Random.Shared.Next(20_000, 40_000);
                var baseUrl = $"http://127.0.0.1:{port}";
                var listener = new HttpListener();
                listener.Prefixes.Add($"{baseUrl}/");
                try
                {
                    listener.Start();
                    return new LocalOidcDiscoveryServer(listener, baseUrl);
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                }
            }

            throw new InvalidOperationException("Could not bind a local HTTP listener for OIDC discovery tests.");
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => WriteResponse(context), CancellationToken.None);
            }
        }

        private void WriteResponse(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            string body;
            if (path.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
            {
                body =
                    $$"""
                    {
                      "issuer": "{{Issuer}}",
                      "authorization_endpoint": "{{AuthorizationEndpoint}}",
                      "token_endpoint": "{{Issuer}}/token",
                      "jwks_uri": "{{Issuer}}/protocol/openid-connect/certs",
                      "response_types_supported": ["code"],
                      "subject_types_supported": ["public"],
                      "id_token_signing_alg_values_supported": ["RS256"]
                    }
                    """;
            }
            else if (path.EndsWith("/protocol/openid-connect/certs", StringComparison.Ordinal))
            {
                body = """{"keys":[]}""";
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            _listener.Close();
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // Listener shutdown can leave GetContextAsync hanging briefly.
            }

            _cts.Dispose();
        }
    }
}
