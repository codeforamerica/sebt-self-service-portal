using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Generates portal JWTs for all authentication paths. Implements three focused
/// interfaces — one per caller — so each entry point owns its claim resolution.
/// A shared <see cref="BuildAndSignToken"/> handles mechanical JWT construction.
/// </summary>
public class JwtTokenService : ILocalLoginTokenService, IOidcTokenService, ISessionRefreshTokenService
{
    private readonly JwtSettings _settings;
    private readonly IdProofingValiditySettings _validitySettings;
    private readonly OidcVerificationClaimTranslator _verificationClaimTranslator;
    private readonly ILogger<JwtTokenService> _logger;

    /// <summary>
    /// Standard OIDC/JWT infrastructure claim names excluded when copying IdP claims.
    /// Parallel to OidcExchangeService.CommonOidcInfrastructureClaims (in Api layer).
    /// </summary>
    private static readonly HashSet<string> OidcInfrastructureClaims =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "iss", "aud", "iat", "exp", "nbf", "nonce", "at_hash", "c_hash",
            "auth_time", "acr", "amr", "azp", "sid", "jti",
            "env", "org", "p1.region"
        };

    /// <summary>
    /// Claim names that BuildAndSignToken sets directly — excluded from the passthrough loop
    /// to avoid duplicates (which .NET's JWT reader rejects).
    /// </summary>
    private static readonly HashSet<string> ReservedClaimNames =
        new(StringComparer.OrdinalIgnoreCase)
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

    public JwtTokenService(
        IOptions<JwtSettings> settings,
        IOptions<IdProofingValiditySettings> validitySettings,
        OidcVerificationClaimTranslator verificationClaimTranslator,
        ILogger<JwtTokenService> logger)
    {
        _settings = settings.Value;
        _validitySettings = validitySettings.Value;
        _verificationClaimTranslator = verificationClaimTranslator;
        _logger = logger;
    }

    // ──────────────────────────────────────────────
    //  ILocalLoginTokenService
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public string GenerateForLocalLogin(User user)
    {
        // Stale data guard: a user with Completed status but IAL ≤ 1 has inconsistent
        // DB state (likely from before the IAL migration). Treat as NotStarted so they
        // can re-verify rather than being blocked from login entirely.
        var effectiveStatus = user.IdProofingStatus;
        if (effectiveStatus == IdProofingStatus.Completed && user.IalLevel < UserIalLevel.IAL1plus)
        {
            _logger.LogError(
                "Inconsistent user state: IdProofingStatus=Completed but IalLevel={IalLevel} for UserId {UserId}. " +
                "Downgrading to NotStarted for JWT. This user's DB record needs correction.",
                user.IalLevel, user.Id);
            effectiveStatus = IdProofingStatus.NotStarted;
        }

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [JwtClaimTypes.Ial] = user.IalLevel switch
            {
                UserIalLevel.IAL1plus => "1plus",
                UserIalLevel.IAL2 => "2",
                _ => "1"
            },
            [JwtClaimTypes.IdProofingStatus] = ((int)effectiveStatus).ToString()
        };

        if (!string.IsNullOrWhiteSpace(user.IdProofingSessionId))
        {
            resolved[JwtClaimTypes.IdProofingSessionId] = user.IdProofingSessionId;
        }

        if (user.IdProofingCompletedAt.HasValue)
        {
            var completedAtOffset = new DateTimeOffset(user.IdProofingCompletedAt.Value, TimeSpan.Zero);
            resolved[JwtClaimTypes.IdProofingCompletedAt] = completedAtOffset.ToUnixTimeSeconds().ToString();

            var expiresAt = user.IdProofingCompletedAt.Value.AddDays(_validitySettings.ValidityDays);
            resolved[JwtClaimTypes.IdProofingExpiresAt] =
                new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString();
        }

        var email = user.Email ?? "";
        return BuildAndSignToken(user.Id, email, resolved);
    }

    // ──────────────────────────────────────────────
    //  IOidcTokenService
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public Result<string> GenerateForOidcLogin(
        User user, ClaimsPrincipal idpPrincipal, bool isStepUp)
    {
        // Filter infrastructure claims from the IdP principal
        var idpClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in idpPrincipal.Claims)
        {
            if (!OidcInfrastructureClaims.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
            {
                idpClaims[claim.Type] = claim.Value;
            }
        }

        // Translate verification claims (e.g., socureIdVerificationLevel → IAL)
        var verification = _verificationClaimTranslator.Translate(idpClaims);

        if (isStepUp && verification == null)
        {
            return Kernel.Result<string>.DependencyFailed(
                Kernel.Results.DependencyFailedReason.BadRequest,
                "Step-up verification failed: IdP returned no verification claims.");
        }

        // Resolve IAL and ID proofing state
        if (verification != null)
        {
            idpClaims[JwtClaimTypes.Ial] = verification.IsExpired
                ? "1"
                : verification.IalLevel switch
                {
                    UserIalLevel.IAL1plus => "1plus",
                    UserIalLevel.IAL2 => "2",
                    _ => "1"
                };

            idpClaims[JwtClaimTypes.IdProofingStatus] =
                (verification.IsExpired, verification.IalLevel) switch
                {
                    (true, _) => ((int)IdProofingStatus.Expired).ToString(),
                    (_, UserIalLevel.IAL1plus) => ((int)IdProofingStatus.Completed).ToString(),
                    (_, UserIalLevel.IAL2) => ((int)IdProofingStatus.Completed).ToString(),
                    _ => ((int)IdProofingStatus.NotStarted).ToString()
                };

            // CompletedAt and ExpiresAt computed together — the gap that caused
            // the step-up loop bug is structurally impossible here.
            if (verification.VerifiedAt != default)
            {
                var verifiedAtOffset = new DateTimeOffset(verification.VerifiedAt, TimeSpan.Zero);
                idpClaims[JwtClaimTypes.IdProofingCompletedAt] =
                    verifiedAtOffset.ToUnixTimeSeconds().ToString();

                var expiresAt = verification.VerifiedAt.AddDays(_validitySettings.ValidityDays);
                idpClaims[JwtClaimTypes.IdProofingExpiresAt] =
                    new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString();
            }
        }
        else
        {
            // No verification claims — user is IAL1 (authenticated but not verified)
            idpClaims[JwtClaimTypes.Ial] = "1";
            idpClaims[JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.NotStarted).ToString();
        }

        var email = idpClaims.GetValueOrDefault("email") ?? user.Email ?? "";
        return Result<string>.Success(BuildAndSignToken(user.Id, email, idpClaims));
    }

    // ──────────────────────────────────────────────
    //  ISessionRefreshTokenService
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public string GenerateForSessionRefresh(User user, ClaimsPrincipal currentPrincipal)
    {
        var existingClaims = currentPrincipal.Claims
            .DistinctBy(c => c.Type)
            .Where(c => !string.IsNullOrEmpty(c.Value))
            .ToDictionary(c => c.Type, c => c.Value, StringComparer.OrdinalIgnoreCase);

        // Resolve IAL: prefer existing JWT claim, fall back to user entity
        if (!existingClaims.ContainsKey(JwtClaimTypes.Ial))
        {
            existingClaims[JwtClaimTypes.Ial] = user.IalLevel switch
            {
                UserIalLevel.IAL1plus => "1plus",
                UserIalLevel.IAL2 => "2",
                _ => "1"
            };
        }

        if (!existingClaims.ContainsKey(JwtClaimTypes.IdProofingStatus))
        {
            existingClaims[JwtClaimTypes.IdProofingStatus] = ((int)user.IdProofingStatus).ToString();
        }

        var email = existingClaims.GetValueOrDefault(JwtRegisteredClaimNames.Email) ?? "";

        return BuildAndSignToken(user.Id, email, existingClaims);
    }

    // ──────────────────────────────────────────────
    //  Shared JWT construction
    // ──────────────────────────────────────────────

    /// <summary>
    /// Construction of JWT from pre-resolved claims. Each public method
    /// fully resolves its claims before calling this — no fallback logic here.
    /// </summary>
    internal string BuildAndSignToken(
        Guid userId,
        string email,
        IReadOnlyDictionary<string, string> resolvedClaims)
    {
        var ialValue = resolvedClaims.GetValueOrDefault(JwtClaimTypes.Ial) ?? "1";
        var idProofingStatusValue = resolvedClaims.GetValueOrDefault(JwtClaimTypes.IdProofingStatus)
            ?? ((int)IdProofingStatus.NotStarted).ToString();

        // Invariant: Completed ID proofing must have IAL > 1 and a completion timestamp.
        if (idProofingStatusValue == ((int)IdProofingStatus.Completed).ToString())
        {
            if (ialValue == "1")
            {
                throw new InvalidOperationException(
                    "Cannot mint JWT with IdProofingStatus=Completed and IAL=1. " +
                    "Completed identity proofing must elevate IAL above 1.");
            }

            if (!resolvedClaims.ContainsKey(JwtClaimTypes.IdProofingCompletedAt))
            {
                throw new InvalidOperationException(
                    "Cannot mint JWT with IdProofingStatus=Completed without a completion timestamp. " +
                    "IdProofingCompletedAt is required to compute expiration.");
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var unixTimeSeconds = now.ToUnixTimeSeconds();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Aud, "SEBT.Portal.Web"),
            new(JwtRegisteredClaimNames.Iss, "SEBT.Portal.Api"),
            new(JwtClaimTypes.IdProofingStatus, idProofingStatusValue, ClaimValueTypes.Integer32),
            new(JwtClaimTypes.Ial, ialValue)
        };

        // Add optional ID proofing claims if present in resolved set
        TryAddClaim(claims, resolvedClaims, JwtClaimTypes.IdProofingSessionId);
        TryAddClaim(claims, resolvedClaims, JwtClaimTypes.IdProofingCompletedAt);
        TryAddClaim(claims, resolvedClaims, JwtClaimTypes.IdProofingExpiresAt);

        // Passthrough remaining application claims (phone, givenName, etc.)
        foreach (var (name, value) in resolvedClaims)
        {
            if (!string.IsNullOrEmpty(name)
                && value != null
                && !ReservedClaimNames.Contains(name)
                && !claims.Any(c => c.Type == name))
            {
                claims.Add(new Claim(name, value));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool TryAddClaim(
        IList<Claim> outgoingClaims,
        IReadOnlyDictionary<string, string> incomingClaims,
        string claimType)
    {
        if (incomingClaims.TryGetValue(claimType, out var claimValue))
        {
            outgoingClaims.Add(new Claim(claimType, claimValue));
            return true;
        }
        return false;
    }
}
