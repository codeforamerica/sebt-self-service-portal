using System.Net;
using System.Net.Http.Headers;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class StateBackendOAuthClientCredentialsAuthHandlerTests
{
    private sealed class StubSecretResolver(string value) : IStateBackendSecretResolver
    {
        public string Resolve(string reference) => value;
    }

    private static StateBackendOAuthClientCredentialsAuthScheme BuildScheme() =>
        new()
        {
            TokenUrl = new Uri("http://backend.test/oauth/token"),
            ClientId = "co-client",
            ClientSecretRef = "StateBackend:Auth:ClientSecret",
        };

    [Fact]
    public async Task FetchesToken_AndAttachesBearer_ToOutgoingRequest()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/oauth/token")
            .Respond("application/json", "{\"access_token\":\"token-abc\",\"token_type\":\"Bearer\",\"expires_in\":3600}");
        mockHttp
            .Expect(HttpMethod.Get, "http://backend.test/data")
            .WithHeaders("Authorization", "Bearer token-abc")
            .Respond(HttpStatusCode.OK);

        var handler = new StateBackendOAuthClientCredentialsAuthHandler(
            BuildScheme(),
            new StubSecretResolver("client-secret-value"),
            mockHttp.ToHttpClient())
        {
            InnerHandler = mockHttp,
        };
        var client = new HttpClient(handler);

        // Act
        HttpResponseMessage response = await client.GetAsync("http://backend.test/data");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CachesToken_AndFetchesOnlyOnce_AcrossMultipleRequests()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        int tokenFetches = 0;
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/oauth/token")
            .Respond(() =>
            {
                Interlocked.Increment(ref tokenFetches);
                var message = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"token-abc\",\"token_type\":\"Bearer\",\"expires_in\":3600}"),
                };
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return Task.FromResult(message);
            });
        mockHttp
            .When(HttpMethod.Get, "http://backend.test/data")
            .Respond(HttpStatusCode.OK);

        var handler = new StateBackendOAuthClientCredentialsAuthHandler(
            BuildScheme(),
            new StubSecretResolver("client-secret-value"),
            mockHttp.ToHttpClient())
        {
            InnerHandler = mockHttp,
        };
        var client = new HttpClient(handler);

        // Act
        await client.GetAsync("http://backend.test/data");
        await client.GetAsync("http://backend.test/data");

        // Assert
        Assert.Equal(1, tokenFetches);
    }
}
