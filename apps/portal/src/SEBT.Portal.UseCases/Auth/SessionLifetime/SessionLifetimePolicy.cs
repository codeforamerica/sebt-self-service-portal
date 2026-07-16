using System.Security.Claims;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.UseCases.Auth.SessionLifetime;

/// <summary>
/// Evaluates whether a portal session has exceeded its absolute lifetime cap, anchored
/// to the JWT <c>auth_time</c> claim (RFC 7519 / OIDC Core).
/// </summary>
/// <remarks>
/// Pure business rule; no I/O. The bearer middleware calls this on every authenticated
/// request, so a session that lacks <c>auth_time</c> or has aged past the cap is rejected
/// before any controller runs — not just at <c>/api/auth/refresh</c>.
/// </remarks>
public class SessionLifetimePolicy(
    IOptions<JwtSettings> jwtSettings,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Standard OIDC claim name. With <c>MapInboundClaims = false</c> the bearer middleware
    /// leaves it as a literal claim type on the principal.
    /// </summary>
    public const string AuthTimeClaimName = "auth_time";

    public enum Outcome
    {
        /// <summary>Session is within its absolute lifetime window.</summary>
        Valid,
        /// <summary>The principal has no <c>auth_time</c> claim (or it's not parseable).</summary>
        MissingAuthTime,
        /// <summary>Session age has reached or passed the configured cap.</summary>
        Expired
    }

    public Outcome Evaluate(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(AuthTimeClaimName)?.Value;
        if (!long.TryParse(claim, out var authTimeUnixSeconds))
        {
            return Outcome.MissingAuthTime;
        }

        var ageSeconds = timeProvider.GetUtcNow().ToUnixTimeSeconds() - authTimeUnixSeconds;
        var capSeconds = jwtSettings.Value.AbsoluteExpirationMinutes * 60L;
        return ageSeconds >= capSeconds ? Outcome.Expired : Outcome.Valid;
    }
}
