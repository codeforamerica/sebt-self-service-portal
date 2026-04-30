namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Constants for JWT claim type names used in the application.
/// </summary>
public static class JwtClaimTypes
{
    /// <summary>
    /// Claim name for the ID proofing status.
    /// </summary>
    public const string IdProofingStatus = "id_proofing_status";

    /// <summary>
    /// Claim name for Identity Assurance Level (IAL).
    /// Values: "1", "1plus", "2"
    /// </summary>
    public const string Ial = "ial";

    /// <summary>
    /// Claim name for the ID proofing session ID.
    /// </summary>
    public const string IdProofingSessionId = "id_proofing_session_id";

    /// <summary>
    /// Claim name for the ID proofing completion timestamp.
    /// </summary>
    public const string IdProofingCompletedAt = "id_proofing_completed_at";

    /// <summary>
    /// Claim name for the ID proofing expiration timestamp.
    /// </summary>
    public const string IdProofingExpiresAt = "id_proofing_expires_at";

    /// <summary>
    /// Claim name for whether the user's record was co-loaded from an external state system.
    /// Values: "true" or "false".
    /// </summary>
    public const string IsCoLoaded = "is_co_loaded";
}
