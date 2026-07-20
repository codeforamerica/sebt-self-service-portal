using System.Security.Claims;

namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Extension methods for resolving <see cref="UserIalLevel"/> from claims.
/// </summary>
public static class UserIalLevelExtensions
{
    /// <summary>
    /// Resolves the user's Identity Assurance Level from their JWT claims.
    /// </summary>
    public static UserIalLevel FromClaimsPrincipal(ClaimsPrincipal user)
    {
        var ialClaim = user.FindFirst(JwtClaimTypes.Ial)?.Value;
        if (string.IsNullOrWhiteSpace(ialClaim)) return UserIalLevel.None;

        return ialClaim.Trim().ToLowerInvariant() switch
        {
            "1" => UserIalLevel.IAL1,
            "1plus" => UserIalLevel.IAL1plus,
            "2" => UserIalLevel.IAL2,
            _ => UserIalLevel.None
        };
    }
}
