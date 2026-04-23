using System.Security.Claims;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Extension methods for reading portal-specific values from <see cref="ClaimsPrincipal"/>.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the portal's internal user ID from the authenticated JWT's <c>sub</c> claim.
    /// Returns null when the claim is absent or does not parse to a Guid (e.g. an
    /// unauthenticated principal, a malformed token, or a legacy int-valued sub from
    /// before the UUID migration — those are treated as invalid and require re-authentication).
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var subValue = principal.FindFirst("sub")?.Value;
        return Guid.TryParse(subValue, out var id) ? id : null;
    }

    /// <summary>
    /// Extracts the user's email address from the JWT claims. Checks the long-form
    /// <see cref="ClaimTypes.Email"/> first (written by <c>JwtTokenService</c>) then the
    /// short-form <c>email</c>. Returns null when absent — OIDC users whose IdP didn't
    /// include an email claim legitimately have no email.
    /// </summary>
    public static string? GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
    }
}
