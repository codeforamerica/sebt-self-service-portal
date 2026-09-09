namespace SEBT.Portal.Api.Services;

/// <summary>
/// Helper for the HttpOnly session cookie that carries the portal JWT.
/// The JWT is never returned in response bodies or exposed to JavaScript —
/// the browser transports it automatically via this cookie.
/// </summary>
public static class AuthCookies
{
    /// <summary>Name of the HttpOnly cookie carrying the portal JWT.</summary>
    public const string AuthCookieName = "sebt_portal_session";

    /// <summary>Path prefix of the public enrollment-checker API (EnrollmentCheckController).</summary>
    private static readonly PathString EnrollmentApiPrefix = new("/api/enrollment");

    /// <summary>
    /// Whether the session cookie may be used to authenticate a request to the
    /// given path. The enrollment-checker API is anonymous by contract and is
    /// excluded: deployments can serve the checker from the portal's origin
    /// (CO production does), so a logged-in household's session cookie rides
    /// along on checker requests and would otherwise attach portal identity to
    /// anonymous enrollment-check logs.
    /// </summary>
    public static bool AllowsCookieAuthentication(PathString requestPath) =>
        !requestPath.StartsWithSegments(EnrollmentApiPrefix);

    /// <summary>Writes the portal JWT to the HttpOnly session cookie; expires with the token.</summary>
    public static void SetAuthCookie(HttpResponse response, string token, DateTimeOffset expiresAt)
    {
        response.Cookies.Append(AuthCookieName, token, BuildCookieOptions(expiresAt));
    }

    /// <summary>Clears the session cookie; attributes must mirror <see cref="SetAuthCookie"/> so the browser deletes it.</summary>
    public static void ClearAuthCookie(HttpResponse response)
    {
        response.Cookies.Delete(AuthCookieName, BuildCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private static CookieOptions BuildCookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true
    };
}
