using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Service for generating JWT tokens for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user, including ID proofing status in claims.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>A JWT token string.</returns>
    string GenerateToken(User user);
}

