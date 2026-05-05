namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response model for GET /api/auth/status. Returned only on 200 — a 401 means the caller
/// is not authenticated. Carries non-sensitive session claims the SPA needs to drive UI
/// decisions (IAL gating, analytics) now that the raw JWT lives in an HttpOnly cookie
/// and cannot be decoded client-side.
/// </summary>
/// <param name="IsAuthorized">Always true when this response is returned (200 OK).</param>
/// <param name="Email">The email address of the authenticated user.</param>
/// <param name="Ial">
/// Identity assurance level claim from the JWT ("0", "1", "1plus", or "2"). Null when unknown.
/// </param>
/// <param name="IdProofingStatus">
/// Workflow state of the user's ID proofing process. See <c>SEBT.Portal.Core.Models.Auth.IdProofingStatus</c>.
/// Null when the claim is absent.
/// </param>
/// <param name="IdProofingCompletedAt">
/// Unix seconds timestamp of the most recent successful ID proofing completion. Null when none.
/// </param>
/// <param name="IdProofingExpiresAt">
/// Unix seconds timestamp after which the IdP-bounded proofing credential should be re-verified. Null when not time-bounded.
/// </param>
/// <param name="IsCoLoaded">
/// Whether the user's record was co-loaded from an external state system. Null when the claim is absent.
/// </param>
/// <param name="ExpiresAt">
/// Unix seconds timestamp at which the current session cookie expires (sliding/idle expiry).
/// The SPA uses this to schedule activity-gated refreshes. Null when the claim is absent.
/// </param>
/// <param name="AbsoluteExpiresAt">
/// Unix seconds timestamp at which the session reaches its absolute lifetime cap, regardless of
/// activity. Computed from the JWT <c>auth_time</c> claim plus <c>JwtSettings.AbsoluteExpirationMinutes</c>.
/// Null when <c>auth_time</c> is absent.
/// </param>
public record AuthorizationStatusResponse(
    bool IsAuthorized,
    string? Email = null,
    string? Ial = null,
    int? IdProofingStatus = null,
    long? IdProofingCompletedAt = null,
    long? IdProofingExpiresAt = null,
    bool? IsCoLoaded = null,
    long? ExpiresAt = null,
    long? AbsoluteExpiresAt = null);

