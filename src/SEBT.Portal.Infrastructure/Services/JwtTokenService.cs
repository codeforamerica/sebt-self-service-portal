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
    private readonly IdProofingValiditySettings _validitySettings;

    public JwtTokenService(
        IOptions<JwtSettings> settings,
        IOptions<IdProofingValiditySettings> validitySettings)
    {
        _settings = settings.Value;
        _validitySettings = validitySettings.Value;
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="additionalClaims">Optional claims to add. For OIDC users, these carry
    /// fresh values from the IdP (email, sub, IAL, ID proofing state) that override stale
    /// DB values. For OTP users, this is typically null and the user object is used.</param>
    /// <returns>A JWT token string.</returns>
    public string GenerateToken(User user, IReadOnlyDictionary<string, string>? additionalClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var unixTimeSeconds = now.ToUnixTimeSeconds();

        // The portal JWT sub is our internal user ID — always. The IdP's sub (for OIDC
        // users) is stored as ExternalProviderId in the DB, not propagated into the JWT.
        var sub = user.Id.ToString();

        // For OIDC users, email comes from IdP claims (via additionalClaims).
        // For OTP users, it comes from user.Email (the DB value).
        var email = additionalClaims?.GetValueOrDefault("email") ?? user.Email ?? "";

        // For OIDC users, IAL comes from IdP claims carried in additionalClaims.
        // For OTP users, it comes from user.IalLevel (the DB value). Any user reaching
        // GenerateToken is authenticated, so the floor is "1" (IAL1) — never "0".
        var ialValue = additionalClaims?.GetValueOrDefault(JwtClaimTypes.Ial)
            ?? user.IalLevel switch
            {
                UserIalLevel.IAL1plus => "1plus",
                UserIalLevel.IAL2 => "2",
                _ => "1"
            };

        var idProofingStatusValue = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingStatus)
            ?? ((int)user.IdProofingStatus).ToString();

        // Invariant: Completed ID proofing must have IAL > 1 and a completion timestamp.
        // Minting a JWT that says "proofing completed" with IAL1 or no timestamp would
        // leave the frontend IalGuard in an unresolvable state.
        if (idProofingStatusValue == ((int)IdProofingStatus.Completed).ToString())
        {
            if (ialValue == "1")
            {
                throw new InvalidOperationException(
                    "Cannot mint JWT with IdProofingStatus=Completed and IAL=1. " +
                    "Completed identity proofing must elevate IAL above 1.");
            }

            var hasCompletedAt = additionalClaims?.ContainsKey(JwtClaimTypes.IdProofingCompletedAt) == true
                || user.IdProofingCompletedAt.HasValue;
            if (!hasCompletedAt)
            {
                throw new InvalidOperationException(
                    "Cannot mint JWT with IdProofingStatus=Completed without a completion timestamp. " +
                    "IdProofingCompletedAt is required to compute expiration.");
            }
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Nbf, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Aud, "SEBT.Portal.Web"),
            new Claim(JwtRegisteredClaimNames.Iss, "SEBT.Portal.Api"),
            // Workflow state of ID proofing process
            new Claim(JwtClaimTypes.IdProofingStatus, idProofingStatusValue, ClaimValueTypes.Integer32),
            // The user's current Identity Assurance Level (IAL)
            new Claim(JwtClaimTypes.Ial, ialValue)
        };

        // Add optional ID proofing claims — prefer additionalClaims, fall back to user properties
        var sessionId = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingSessionId)
            ?? user.IdProofingSessionId;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingSessionId, sessionId));
        }

        var completedAtStr = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingCompletedAt);
        if (completedAtStr != null)
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingCompletedAt, completedAtStr, ClaimValueTypes.Integer64));
        }
        else if (user.IdProofingCompletedAt.HasValue)
        {
            var completedAtOffset = new DateTimeOffset(user.IdProofingCompletedAt.Value, TimeSpan.Zero);
            claims.Add(new Claim(JwtClaimTypes.IdProofingCompletedAt,
                completedAtOffset.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        }

        // Compute expiration from completedAt + validity, regardless of source
        var expiresAtStr = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingExpiresAt);
        if (expiresAtStr != null)
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt, expiresAtStr, ClaimValueTypes.Integer64));
        }
        else if (completedAtStr != null && long.TryParse(completedAtStr, out var completedAtUnix))
        {
            var completedAt = DateTimeOffset.FromUnixTimeSeconds(completedAtUnix).UtcDateTime;
            var expiresAt = completedAt.AddDays(_validitySettings.ValidityDays);
            var expiresAtOffset = new DateTimeOffset(expiresAt, TimeSpan.Zero);
            claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt,
                expiresAtOffset.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        }
        else if (user.IdProofingCompletedAt.HasValue)
        {
            var expiresAt = user.IdProofingCompletedAt.Value.AddDays(_validitySettings.ValidityDays);
            var expiresAtOffset = new DateTimeOffset(expiresAt, TimeSpan.Zero);
            claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt,
                expiresAtOffset.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        }

        // Claim names already set above; do not add again from additionalClaims
        // or the JWT payload would have e.g. "sub": [a, b], which .NET's reader rejects (expects string).
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            JwtRegisteredClaimNames.Sub,
            ClaimTypes.Email,
            JwtRegisteredClaimNames.Email,
            "email",
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Aud,
            JwtRegisteredClaimNames.Iss,
            JwtClaimTypes.Ial,
            JwtClaimTypes.IdProofingStatus,
            JwtClaimTypes.IdProofingSessionId,
            JwtClaimTypes.IdProofingCompletedAt,
            JwtClaimTypes.IdProofingExpiresAt,
        };

        if (additionalClaims != null)
        {
            foreach (var (name, value) in additionalClaims)
            {
                if (!string.IsNullOrEmpty(name) &&
                    value != null &&
                    !reservedNames.Contains(name) &&
                    !claims.Select(c => c.Type).Contains(name))
                {
                    claims.Add(new Claim(name, value));
                }
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

