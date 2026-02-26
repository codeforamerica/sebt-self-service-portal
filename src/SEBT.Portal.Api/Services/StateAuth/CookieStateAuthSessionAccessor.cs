using Microsoft.AspNetCore.Http;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Services.StateAuth;

/// <summary>
/// Provides the current request's state auth session id from a cookie.
/// The host uses this with <see cref="IStateAuthStore"/> to resolve the session's auth context and pass it into <see cref="IStateAuthService"/> plugin methods.
/// </summary>
internal sealed class CookieStateAuthSessionAccessor : IStateAuthSessionAccessor
{
    public const string CookieName = "StateAuth.SessionId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieStateAuthSessionAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentSessionId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Request.Cookies.TryGetValue(CookieName, out var sessionId) == true)
            return sessionId;
        return null;
    }
}
