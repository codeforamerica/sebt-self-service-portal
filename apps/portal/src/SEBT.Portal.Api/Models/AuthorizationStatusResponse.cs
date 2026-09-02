namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response model for GET /api/auth/status. Carries non-sensitive session claims the SPA
/// needs to drive UI decisions (IAL gating, analytics) now that the raw JWT lives in an
/// HttpOnly cookie and cannot be decoded client-side. Anonymous callers get this same
/// shape with <c>IsAuthorized</c> false and every claim null.
/// </summary>
/// <param name="IsAuthorized">
/// True when the caller has a valid session; false for anonymous callers (no cookie, or an
/// expired/tampered/revoked session), in which case all other fields are null.
/// </param>
/// <param name="UserId">
/// Stable, non-PII portal user identifier (the portal's own user UUID). Surfaced for
/// analytics so events can be correlated per-user across page loads without exposing
/// email or other PII to vendor tooling. Null when the claim is absent.
/// </param>
/// <param name="Email">
/// The email address of the signed-in user. Null for anonymous callers, and null when
/// the claim is absent from the session.
/// </param>
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
    Guid? UserId = null,
    string? Email = null,
    string? Ial = null,
    int? IdProofingStatus = null,
    long? IdProofingCompletedAt = null,
    long? IdProofingExpiresAt = null,
    bool? IsCoLoaded = null,
    long? ExpiresAt = null,
    long? AbsoluteExpiresAt = null);

