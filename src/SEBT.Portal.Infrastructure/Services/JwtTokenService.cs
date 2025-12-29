using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service responsible for generating JWT tokens for authenticated users.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Generates a JWT token for the specified email address.
    /// </summary>
    /// <param name="email">The email address of the authenticated user.</param>
    /// <returns>A JWT token string.</returns>
    public string GenerateToken(string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var unixTimeSeconds = now.ToUnixTimeSeconds();

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Nbf, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

