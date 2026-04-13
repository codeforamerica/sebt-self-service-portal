namespace SEBT.Portal.Api.Services;

/// <summary>
/// helper for the <c>oidc_session</c> HttpOnly cookie that binds the
/// pre-auth OIDC session to the browser. Short-lived (15 min, matching
/// <see cref="PreAuthSessionStore"/> TTL). Cleared after <c>complete-login</c>
/// because the session has served its purpose by then.
/// </summary>
public static class OidcSessionCookie
{
    /// <summary>Name of the pre-auth session cookie.</summary>
    public const string CookieName = "oidc_session";

    /// <summary>Pre-auth session TTL — matches the HybridCache TTL in <see cref="PreAuthSessionStore"/>.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    /// <summary>Writes the pre-auth session ID to the <c>oidc_session</c> cookie.</summary>
    public static void Set(HttpResponse response, string sessionId)
    {
        response.Cookies.Append(CookieName, sessionId, BuildOptions(DateTimeOffset.UtcNow.Add(MaxAge)));
    }

    /// <summary>Clears the <c>oidc_session</c> cookie after login completes.</summary>
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, BuildOptions(DateTimeOffset.UnixEpoch));
    }

    /// <summary>Reads the session ID from the incoming <c>oidc_session</c> cookie.</summary>
    public static string? Read(HttpRequest request)
    {
        return request.Cookies.TryGetValue(CookieName, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;
    }

    private static CookieOptions BuildOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        // Strict: the cookie is only needed on same-origin POSTs (callback + complete-login),
        // not on the IdP's top-level redirect back to /callback (which is a page load, not a
        // cookie-bearing request). The subsequent fetch POST is same-origin, so Strict allows it.
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expires,
        IsEssential = true
    };
}
