using System.Security.Claims;

namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Extension methods for resolving authentication-related claims from a ClaimsPrincipal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Resolves the user's Identity Assurance Level from the IAL claim.
    /// Returns <see cref="UserIalLevel.None"/> if the claim is missing or unrecognized.
    /// </summary>
    public static UserIalLevel GetIalLevel(this ClaimsPrincipal user)
    {
        var ialClaim = user.FindFirst(JwtClaimTypes.Ial)?.Value;

        if (string.IsNullOrWhiteSpace(ialClaim))
        {
            return UserIalLevel.None;
        }

        var normalized = ialClaim.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1" => UserIalLevel.IAL1,
            "1plus" => UserIalLevel.IAL1plus,
            "2" => UserIalLevel.IAL2,
            "0" => UserIalLevel.None,
            _ => UserIalLevel.None
        };
    }
}
