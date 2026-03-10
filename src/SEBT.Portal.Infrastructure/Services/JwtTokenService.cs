using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
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
    /// Generates a JWT token for the specified user.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="additionalClaims">Optional claims to add</param>
    /// <returns>A JWT token string.</returns>
    public string GenerateToken(User user, IReadOnlyDictionary<string, string>? additionalClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var unixTimeSeconds = now.ToUnixTimeSeconds();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Nbf, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            // Workflow state of ID proofing process
            new Claim(JwtClaimTypes.IdProofingStatus, ((int)user.IdProofingStatus).ToString(), ClaimValueTypes.Integer32),
            // The Users's current Identity Assurance Level (IAL)
            new Claim(JwtClaimTypes.Ial, user.IalLevel switch
            {
                UserIalLevel.IAL1 => "1",
                UserIalLevel.IAL1plus => "1plus",
                UserIalLevel.IAL2 => "2",
                _ => "0" // None
            })
        };

        // Add optional ID proofing claims if available
        if (!string.IsNullOrWhiteSpace(user.IdProofingSessionId))
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingSessionId, user.IdProofingSessionId));
        }

        if (user.IdProofingCompletedAt.HasValue)
        {
            var completedAtOffset = new DateTimeOffset(user.IdProofingCompletedAt.Value, TimeSpan.Zero);
            claims.Add(new Claim(JwtClaimTypes.IdProofingCompletedAt,
                completedAtOffset.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        }

        if (user.IdProofingExpiresAt.HasValue)
        {
            var expiresAtOffset = new DateTimeOffset(user.IdProofingExpiresAt.Value, TimeSpan.Zero);
            claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt,
                expiresAtOffset.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        }

        // Portal-defined claim names we already set above; do not add again from additionalClaims
        // or the JWT payload would have e.g. "sub": [a, b], which .NET's reader rejects (expects string).
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            JwtRegisteredClaimNames.Sub,
            ClaimTypes.Email,
            "email",
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf
        };

        if (additionalClaims != null)
        {
            foreach (var (name, value) in additionalClaims)
            {
                if (!string.IsNullOrEmpty(name) && value != null && !reservedNames.Contains(name))
                    claims.Add(new Claim(name, value));
            }
        }

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

