using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for logout-time JWT revocation: GET /api/auth/logout denylists the
/// session token's jti, and the bearer middleware rejects denylisted tokens on every
/// authenticated request while leaving other sessions untouched.
/// The shared factory configures OIDC endpoints, so logout always enters the OIDC branch;
/// each test substitutes IOidcExchangeService to keep discovery hermetic (no network):
/// a failing substitute exercises the /login fallback, a succeeding one the IdP
/// end-session redirect. Each test derives its factory once so every request in the
/// test shares one service provider (and therefore one denylist cache).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class LogoutTokenDenylistTests : IClassFixture<PortalWebApplicationFactory>
{
    private const string JwtIssuer = "SEBT.Portal.Api";
    private const string JwtAudience = "SEBT.Portal.Web";
    private const string EndSessionEndpoint = "https://idp.example.com/end-session";

    private readonly PortalWebApplicationFactory _factory;

    public LogoutTokenDenylistTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_AfterLogout_ReturnsUnauthenticatedForTheSameToken()
    {
        using var factory = CreateFactoryWhereDiscoveryFails();
        var token = CreateValidJwt(email: "logout-a@example.com");
        Assert.True((await ReadStatus(factory, token)).IsAuthorized);

        using var logoutResponse = await LogoutWithCookie(factory, token);
        Assert.Equal(HttpStatusCode.Found, logoutResponse.StatusCode);

        // The status probe answers anonymous callers with 200 { isAuthorized: false };
        // the revoked token must yield that anonymous shape, never a session.
        var revokedStatus = await ReadStatus(factory, token);
        Assert.False(revokedStatus.IsAuthorized);
        Assert.Null(revokedStatus.Email);

        // Revocation must apply regardless of how the token is presented — the same jti
        // is denylisted whether the SPA sends it via cookie or a service-to-service
        // caller sends it via the Authorization header.
        using var bearerStatusResponse = await GetStatusWithBearerToken(factory, token);
        var bearerStatus = await bearerStatusResponse.Content.ReadFromJsonAsync<AuthorizationStatusResponse>();
        Assert.False(bearerStatus!.IsAuthorized);
    }

    [Fact]
    public async Task Logout_DoesNotAffectOtherSessions()
    {
        using var factory = CreateFactoryWhereDiscoveryFails();
        var tokenA = CreateValidJwt(email: "logout-b@example.com");
        var tokenB = CreateValidJwt(email: "bystander@example.com");

        using var logoutResponse = await LogoutWithCookie(factory, tokenA);
        Assert.Equal(HttpStatusCode.Found, logoutResponse.StatusCode);

        Assert.True((await ReadStatus(factory, tokenB)).IsAuthorized);
    }

    [Fact]
    public async Task Logout_WithoutCookie_StillRedirectsToLogin()
    {
        using var factory = CreateFactoryWhereDiscoveryFails();

        using var response = await LogoutWithCookie(factory, token: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("aaa.bbb.ccc")]
    public async Task Logout_WithMalformedToken_StillRedirectsAndClearsCookie(string malformedToken)
    {
        using var factory = CreateFactoryWhereDiscoveryFails();

        using var response = await LogoutWithCookie(factory, malformedToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location!.ToString());
        AssertAuthCookieCleared(response);
    }

    [Fact]
    public async Task Logout_WithTokenLackingJti_StillRedirectsAndClearsCookie()
    {
        using var factory = CreateFactoryWhereDiscoveryFails();
        var token = CreateValidJwt(email: "no-jti@example.com", includeJti: false);

        using var response = await LogoutWithCookie(factory, token);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location!.ToString());
        AssertAuthCookieCleared(response);
    }

    [Fact]
    public async Task Logout_WithValidToken_ClearsCookie()
    {
        using var factory = CreateFactoryWhereDiscoveryFails();

        using var response = await LogoutWithCookie(factory, CreateValidJwt(email: "logout-c@example.com"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        AssertAuthCookieCleared(response);
    }

    [Fact]
    public async Task Logout_WhenOidcConfigured_RedirectsToIdpEndSession()
    {
        using var factory = CreateFactoryWhereDiscoverySucceeds();
        var token = CreateValidJwt(email: "oidc-logout@example.com");

        using var logoutResponse = await LogoutWithCookie(factory, token);

        Assert.Equal(HttpStatusCode.Found, logoutResponse.StatusCode);
        var location = logoutResponse.Headers.Location!.ToString();
        Assert.StartsWith($"{EndSessionEndpoint}?client_id=", location, StringComparison.Ordinal);
        Assert.Contains("post_logout_redirect_uri=", location, StringComparison.Ordinal);
        AssertAuthCookieCleared(logoutResponse);

        // Revocation happens regardless of which redirect path logout takes.
        Assert.False((await ReadStatus(factory, token)).IsAuthorized);
    }

    /// <summary>Fetches the status probe with the given session cookie and returns its body.</summary>
    private static async Task<AuthorizationStatusResponse> ReadStatus(
        WebApplicationFactory<Program> factory, string? token)
    {
        using var response = await GetStatusWithCookie(factory, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<AuthorizationStatusResponse>();
        Assert.NotNull(status);
        return status;
    }

    private static void AssertAuthCookieCleared(HttpResponseMessage response)
    {
        var setCookies = response.Headers.GetValues("Set-Cookie");
        Assert.Contains(setCookies, c =>
            c.StartsWith($"{AuthCookies.AuthCookieName}=", StringComparison.Ordinal)
            && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Derives a factory whose IOidcExchangeService throws on discovery, deterministically
    /// exercising the /login fallback without depending on the network.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWhereDiscoveryFails()
    {
        var oidcExchangeService = Substitute.For<IOidcExchangeService>();
        oidcExchangeService.GetDiscoveryInfoAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("discovery unavailable"));
        return CreateFactoryWithOidcExchangeService(oidcExchangeService);
    }

    /// <summary>
    /// Derives a factory whose IOidcExchangeService returns discovery info with an
    /// end_session_endpoint, exercising the RP-initiated logout redirect.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWhereDiscoverySucceeds()
    {
        var oidcExchangeService = Substitute.For<IOidcExchangeService>();
        oidcExchangeService.GetDiscoveryInfoAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new OidcDiscoveryInfo { EndSessionEndpoint = EndSessionEndpoint });
        return CreateFactoryWithOidcExchangeService(oidcExchangeService);
    }

    private WebApplicationFactory<Program> CreateFactoryWithOidcExchangeService(
        IOidcExchangeService oidcExchangeService) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(oidcExchangeService)));

    private static HttpClient CreateNoRedirectClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpResponseMessage> GetStatusWithCookie(
        WebApplicationFactory<Program> factory, string? token)
    {
        var client = CreateNoRedirectClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/status");
        if (token != null)
        {
            request.Headers.Add("Cookie", $"{AuthCookies.AuthCookieName}={token}");
        }
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetStatusWithBearerToken(
        WebApplicationFactory<Program> factory, string token)
    {
        var client = CreateNoRedirectClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> LogoutWithCookie(
        WebApplicationFactory<Program> factory, string? token)
    {
        var client = CreateNoRedirectClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/logout");
        if (token != null)
        {
            request.Headers.Add("Cookie", $"{AuthCookies.AuthCookieName}={token}");
        }
        return await client.SendAsync(request);
    }

    private static string CreateValidJwt(string email, int expiresInMinutes = 60, bool includeJti = true)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PortalWebApplicationFactory.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var claims = new List<Claim>
        {
            new("email", email),
            new("sub", email),
            new("auth_time", nowUnixSeconds)
        };
        if (includeJti)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
        }
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(expiresInMinutes),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
