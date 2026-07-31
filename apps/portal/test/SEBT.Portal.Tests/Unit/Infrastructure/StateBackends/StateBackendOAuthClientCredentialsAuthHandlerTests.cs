using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class StateBackendOAuthClientCredentialsAuthHandlerTests
{
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

    // Wires a counting token endpoint + data endpoint behind a handler driven by the fake clock.
    private static (HttpClient Client, Func<int> TokenFetches) BuildClientWithCountingTokenEndpoint(
        FakeTimeProvider timeProvider)
    {
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
            mockHttp.ToHttpClient(),
            timeProvider)
        {
            InnerHandler = mockHttp,
        };
        return (new HttpClient(handler), () => tokenFetches);
    }

    // The handler refreshes ExpiryLeeway (30s) BEFORE the token's actual expiry, so with
    // expires_in=3600 the cached token is reused until +3570s and refetched after.
    [Theory]
    [InlineData(3569, 1)] // just before the refresh point: cached token reused
    [InlineData(3571, 2)] // just past the refresh point: token refetched
    public async Task RefetchesToken_OnlyWhenClockPassesExpiryMinusLeeway(
        int advanceSeconds, int expectedTokenFetches)
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        (HttpClient client, Func<int> tokenFetches) =
            BuildClientWithCountingTokenEndpoint(timeProvider);

        // Act
        await client.GetAsync("http://backend.test/data");
        timeProvider.Advance(TimeSpan.FromSeconds(advanceSeconds));
        await client.GetAsync("http://backend.test/data");

        // Assert
        Assert.Equal(expectedTokenFetches, tokenFetches());
    }

    [Fact]
    public async Task TokenEndpointFailure_Surfaces_WithoutLeakingClientSecret()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/oauth/token")
            .Respond(HttpStatusCode.InternalServerError);

        var handler = new StateBackendOAuthClientCredentialsAuthHandler(
            BuildScheme(),
            new StubSecretResolver("client-secret-value"),
            mockHttp.ToHttpClient())
        {
            InnerHandler = mockHttp,
        };
        var client = new HttpClient(handler);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("http://backend.test/data"));
        Assert.DoesNotContain("client-secret-value", ex.Message);
    }

    [Fact]
    public async Task ScopeConfigured_IsCarriedInTokenRequestForm()
    {
        // Arrange
        StateBackendOAuthClientCredentialsAuthScheme scheme = BuildScheme() with
        {
            Scope = "sebt.read",
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/oauth/token")
            .WithFormData("grant_type", "client_credentials")
            .WithFormData("client_id", "co-client")
            .WithFormData("scope", "sebt.read")
            .Respond("application/json", "{\"access_token\":\"token-abc\",\"token_type\":\"Bearer\",\"expires_in\":3600}");
        mockHttp
            .Expect(HttpMethod.Get, "http://backend.test/data")
            .Respond(HttpStatusCode.OK);

        var handler = new StateBackendOAuthClientCredentialsAuthHandler(
            scheme,
            new StubSecretResolver("client-secret-value"),
            mockHttp.ToHttpClient())
        {
            InnerHandler = mockHttp,
        };
        var client = new HttpClient(handler);

        // Act
        await client.GetAsync("http://backend.test/data");

        // Assert
        mockHttp.VerifyNoOutstandingExpectation();
    }

    private static HttpClient BuildClientWithTokenBody(string tokenBody)
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/oauth/token")
            .Respond("application/json", tokenBody);
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
        return new HttpClient(handler);
    }

    // The JSON literal null deserializes without error to a null response → the explicit throw.
    [Fact]
    public async Task TokenBodyIsJsonNull_ThrowsInvalidOperation()
    {
        HttpClient client = BuildClientWithTokenBody("null");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("http://backend.test/data"));
        Assert.Contains("empty response", ex.Message);
    }

    [Fact]
    public async Task TokenBodyIsNotJson_ThrowsJsonException()
    {
        HttpClient client = BuildClientWithTokenBody("plainly not json");

        await Assert.ThrowsAsync<JsonException>(
            () => client.GetAsync("http://backend.test/data"));
    }

    private sealed class StubSecretResolver(string value) : IStateBackendSecretResolver
    {
        public string Resolve(string reference) => value;
    }
}
