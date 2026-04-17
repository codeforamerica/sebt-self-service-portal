using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for the JwtBearer cookie fallback wired in Program.cs.
/// DC-242 stores the portal session JWT in an HttpOnly cookie instead of
/// returning it in response bodies; the JwtBearer middleware reads it from
/// that cookie when no Authorization header is present. These tests exercise
/// the real HTTP pipeline to confirm the fallback authenticates valid sessions
/// and rejects requests without (or with tampered) cookies.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AuthCookieAuthenticationTests : IClassFixture<PortalWebApplicationFactory>
{
    private const string JwtIssuer = "SEBT.Portal.Api";
    private const string JwtAudience = "SEBT.Portal.Web";

    private readonly PortalWebApplicationFactory _factory;

    public AuthCookieAuthenticationTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStatus_WithValidSessionCookie_ReturnsOk()
    {
        using var response = await GetStatusWithCookie(CreateValidJwt(email: "user@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WithoutSessionCookie_ReturnsUnauthorized()
    {
        using var response = await GetStatusWithCookie(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WithTamperedSessionCookie_ReturnsUnauthorized()
    {
        var token = CreateValidJwt(email: "user@example.com");
        // Replace the last 4 chars of the signature with garbage so verification fails.
        var tampered = token[..^4] + "XXXX";

        using var response = await GetStatusWithCookie(tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WithExpiredSessionCookie_ReturnsUnauthorized()
    {
        var token = CreateValidJwt(email: "user@example.com", expiresInMinutes: -10);

        using var response = await GetStatusWithCookie(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetStatusWithCookie(string? token)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/status");
        if (token != null)
            request.Headers.Add("Cookie", $"{AuthCookies.AuthCookieName}={token}");
        return await client.SendAsync(request);
    }

    private static string CreateValidJwt(string email, int expiresInMinutes = 60)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PortalWebApplicationFactory.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("email", email),
            new Claim("sub", email)
        };
        var now = DateTime.UtcNow;
        // notBefore must precede expires; pad it well behind expires to cover negative
        // expiresInMinutes used by the expired-token test.
        var notBefore = now.AddMinutes(Math.Min(-1, expiresInMinutes - 1));
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: notBefore,
            expires: now.AddMinutes(expiresInMinutes),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
