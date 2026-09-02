namespace SEBT.Portal.Core.Services;

/// <summary>
/// Structured logging for OIDC callback failures that send users to id-proofing off-boarding.
/// </summary>
public interface IOidcCallbackFailureLogger
{
    /// <summary>
    /// Returns true when <paramref name="reason"/> may be submitted via
    /// <c>POST /api/auth/oidc/report-failure</c>.
    /// </summary>
    bool IsAllowedClientReason(string reason);

    /// <summary>
    /// Logs a single correlated warning for operations and support.
    /// </summary>
    void Log(OidcCallbackFailureLogEntry entry);
}

/// <summary>Fields attached to an off-boarding OIDC failure log line.</summary>
public sealed record OidcCallbackFailureLogEntry
{
    /// <summary>
    /// Machine-readable cause. Examples: <c>idp_redirect</c>, <c>missing_params</c>,
    /// <c>token_exchange_rejected</c>, <c>missing_session</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>OAuth <c>error</c> from an IdP redirect or token response.</summary>
    public string? IdpError { get; init; }

    /// <summary>OAuth <c>error_description</c> (sanitized and truncated before logging).</summary>
    public string? IdpErrorDescription { get; init; }

    /// <summary>
    /// HTTP status when the failure came from an API response. Pass-through diagnostic
    /// data for the off-boarding dashboards — populated by whichever layer knows the
    /// status, never interpreted by inner layers.
    /// </summary>
    public int? HttpStatus { get; init; }

    /// <summary>Portal error message returned to the browser (sanitized).</summary>
    public string? ApiError { get; init; }

    /// <summary>Pre-auth session id when available.</summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Portal user GUID when known. When omitted, resolved from the authenticated JWT on
    /// the current HTTP request (e.g. step-up while already signed in). Absent on first login.
    /// </summary>
    public Guid? PortalUserId { get; init; }

    /// <summary>Whether the flow was step-up.</summary>
    public bool? IsStepUp { get; init; }

    /// <summary><c>callback</c> or <c>complete-login</c> when the failure is tied to an API phase.</summary>
    public string? Phase { get; init; }

    /// <summary>For <c>missing_params</c>: whether the authorization <c>code</c> query param was present.</summary>
    public bool? HasCode { get; init; }

    /// <summary>For <c>missing_params</c>: whether the OAuth <c>state</c> query param was present.</summary>
    public bool? HasState { get; init; }
}
