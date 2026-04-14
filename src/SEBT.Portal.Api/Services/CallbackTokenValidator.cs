using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Services;

/// <summary>
/// Validates OIDC callback tokens (signed JWTs produced by <see cref="OidcExchangeService"/>)
/// and extracts non-infrastructure claims for the portal JWT.
/// </summary>
public sealed class CallbackTokenValidator(
    IConfiguration config,
    ILogger<CallbackTokenValidator> logger) : ICallbackTokenValidator
{
    /// <inheritdoc />
    public CallbackTokenValidationResult Validate(string callbackToken)
    {
        var signingKey = config["Oidc:CompleteLoginSigningKey"];
        if (string.IsNullOrEmpty(signingKey))
        {
            logger.LogWarning("Oidc:CompleteLoginSigningKey is not configured.");
            return CallbackTokenValidationResult.ServerConfigError(
                "Complete-login not configured.");
        }

        var portalOrigin = config["Oidc:CallbackRedirectUri"]?.TrimEnd('/') ?? "sebt-portal";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = portalOrigin,
            ValidateAudience = true,
            ValidAudience = portalOrigin,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Use resolver instead of IssuerSigningKey to bypass kid-matching;
            // the callback token is signed without a kid header, which causes IDX10517
            // when JwtSecurityTokenHandler tries to match by kid.
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => [key]
        };
        var handler = new JwtSecurityTokenHandler();
        handler.MapInboundClaims = false; // Preserve original JWT claim names (sub, email)

        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(callbackToken, validationParams, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid or expired callback token");
            return CallbackTokenValidationResult.InvalidToken(
                "Invalid or expired callback token.");
        }

        // Extract non-infrastructure claims for passthrough to the portal JWT
        var additionalClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
        {
            if (!OidcExchangeService.CommonOidcInfrastructureClaims.Contains(claim.Type)
                && !string.IsNullOrEmpty(claim.Value))
            {
                additionalClaims[claim.Type] = claim.Value;
            }
        }

        logger.LogInformation(
            "Additional OIDC claim types: {Claims}",
            string.Join(", ", additionalClaims.Keys));

        if (!additionalClaims.ContainsKey("phone"))
        {
            logger.LogWarning("OIDC incoming claims missing 'phone'");
        }

        // Extract email (or sub as fallback)
        var email = principal.FindFirst("email")?.Value;
        if (string.IsNullOrEmpty(email))
        {
            email = principal.FindFirst("sub")?.Value;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Callback token had no email or sub claim");
            return CallbackTokenValidationResult.InvalidToken(
                "Callback token must contain an email or sub claim.");
        }

        return CallbackTokenValidationResult.Success(
            EmailNormalizer.Normalize(email),
            additionalClaims);
    }
}
