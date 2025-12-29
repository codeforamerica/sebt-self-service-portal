namespace SEBT.Portal.Core.Services;

/// <summary>
/// Service for generating JWT tokens for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the specified email address.
    /// </summary>
    /// <param name="email">The email address of the authenticated user.</param>
    /// <returns>A JWT token string.</returns>
    string GenerateToken(string email);
}

