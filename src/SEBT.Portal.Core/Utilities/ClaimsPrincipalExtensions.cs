using System.Security.Claims;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Extension methods for reading portal-specific values from <see cref="ClaimsPrincipal"/>.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the portal's internal user ID from the authenticated JWT's <c>sub</c> claim.
    /// Returns null when the claim is absent or does not parse to a positive integer
    /// (e.g. an unauthenticated principal or a malformed token).
    /// </summary>
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var subValue = principal.FindFirst("sub")?.Value;
        return int.TryParse(subValue, out var id) && id > 0 ? id : null;
    }
}
