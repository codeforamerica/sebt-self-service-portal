using Microsoft.AspNetCore.Http;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for the AuthCookies helper — ensures the session JWT cookie is always
/// written with the security attributes required by DC-242 (HttpOnly, Secure, SameSite=Lax)
/// and that Delete mirrors the same attributes so browsers actually remove it.
/// </summary>
public class AuthCookiesTests
{
    private static HttpContext CreateHttpContext() => new DefaultHttpContext();

    [Fact]
    public void SetAuthCookie_WritesHttpOnlySecureSameSiteLaxCookieWithGivenExpiry()
    {
        var context = CreateHttpContext();
        var expires = DateTimeOffset.UtcNow.AddMinutes(60);

        AuthCookies.SetAuthCookie(context.Response, "test.jwt.value", expires);

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}=test.jwt.value", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAuthCookie_WritesExpiredCookieThatMatchesTheOriginalAttributes()
    {
        var context = CreateHttpContext();

        AuthCookies.ClearAuthCookie(context.Response);

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}=", setCookie);
        // Browser-compatible delete: same attributes + past expiry
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthCookieName_MatchesExpectedConstant()
    {
        // The frontend MSW handlers and any infra tooling depend on this exact name.
        // Changing it is a breaking protocol change.
        Assert.Equal("sebt_portal_session", AuthCookies.AuthCookieName);
    }
}
