using Microsoft.AspNetCore.Http;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// verifies the oidc_session cookie is set with security attributes
/// matching the sebt_portal_session cookie pattern (HttpOnly, Secure, SameSite),
/// and that Read/Clear round-trip correctly.
/// </summary>
public class OidcSessionCookieTests
{
    [Fact]
    public void Set_WritesHttpOnlySecureSameSiteStrictCookie()
    {
        var context = new DefaultHttpContext();

        OidcSessionCookie.Set(context.Response, "test-session-id");

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{OidcSessionCookie.CookieName}=test-session-id", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Clear_WritesExpiredCookieWithMatchingAttributes()
    {
        var context = new DefaultHttpContext();

        OidcSessionCookie.Clear(context.Response);

        var setCookie = context.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{OidcSessionCookie.CookieName}=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_ReturnsSessionIdFromCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{OidcSessionCookie.CookieName}=abc123";

        var result = OidcSessionCookie.Read(context.Request);

        Assert.Equal("abc123", result);
    }

    [Fact]
    public void Read_ReturnsNullWhenCookieMissing()
    {
        var context = new DefaultHttpContext();

        var result = OidcSessionCookie.Read(context.Request);

        Assert.Null(result);
    }

    [Fact]
    public void Read_ReturnsNullWhenCookieEmpty()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{OidcSessionCookie.CookieName}=";

        var result = OidcSessionCookie.Read(context.Request);

        Assert.Null(result);
    }

    [Fact]
    public void CookieName_MatchesExpectedConstant()
    {
        Assert.Equal("oidc_session", OidcSessionCookie.CookieName);
    }
}
