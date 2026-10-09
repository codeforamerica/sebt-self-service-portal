using System.Net;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class StateBackendApiKeyAuthHandlerTests
{
    [Fact]
    public async Task SetsConfiguredHeaderToResolvedKey_OnOutgoingRequest()
    {
        // Arrange
        var scheme = new StateBackendApiKeyAuthScheme
        {
            Header = "X-Api-Key",
            KeyRef = "StateBackend:Auth:ApiKey",
        };
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Get, "http://backend.test/data")
            .WithHeaders("X-Api-Key", "resolved-secret-value")
            .Respond(HttpStatusCode.OK);

        var handler = new StateBackendApiKeyAuthHandler(scheme, new StubSecretResolver("resolved-secret-value"))
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

    private sealed class StubSecretResolver(string value) : IStateBackendSecretResolver
    {
        public string Resolve(string reference) => value;
    }
}
