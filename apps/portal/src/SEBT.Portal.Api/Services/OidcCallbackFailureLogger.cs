using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Services;

/// <inheritdoc cref="IOidcCallbackFailureLogger"/>
public sealed class OidcCallbackFailureLogger(
    ILogger<OidcCallbackFailureLogger> logger,
    IHttpContextAccessor httpContextAccessor) : IOidcCallbackFailureLogger
{
    private static readonly HashSet<string> AllowedClientReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "idp_redirect",
        "missing_params"
    };

    /// <inheritdoc/>
    public bool IsAllowedClientReason(string reason) => AllowedClientReasons.Contains(reason);

    /// <inheritdoc/>
    public void Log(OidcCallbackFailureLogEntry entry)
    {
        var portalUserId = entry.PortalUserId ?? httpContextAccessor.HttpContext?.User.GetUserId();

        logger.LogWarning(
            "OIDC callback off-boarding: Reason={Reason} PortalUserId={PortalUserId} IdpError={IdpError} "
            + "IdpErrorDescription={IdpErrorDescription} HttpStatus={HttpStatus} ApiError={ApiError} "
            + "SessionId={SessionId} IsStepUp={IsStepUp} Phase={Phase} HasCode={HasCode} HasState={HasState}",
            entry.Reason,
            portalUserId,
            OidcLogSanitizer.Sanitize(entry.IdpError, OidcLogSanitizer.MaxErrorCodeLength),
            OidcLogSanitizer.Sanitize(entry.IdpErrorDescription),
            entry.HttpStatus,
            OidcLogSanitizer.Sanitize(entry.ApiError),
            entry.SessionId,
            entry.IsStepUp,
            OidcLogSanitizer.Sanitize(entry.Phase),
            entry.HasCode,
            entry.HasState);
    }
}
