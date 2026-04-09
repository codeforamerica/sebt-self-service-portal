namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Represents a user in the system with ID proofing status.
/// </summary>
public class User
{
    /// <summary>
    /// The unique identifier for the user (database primary key).
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// The user's email address, used as a unique identifier.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Workflow state of the ID proofing process (NotStarted, InProgress, Completed, Failed, Expired).
    /// </summary>
    public IdProofingStatus IdProofingStatus { get; set; } = IdProofingStatus.NotStarted;

    /// <summary>
    /// The Identity Assurance Level (IAL) this user has achieved through ID proofing.
    /// </summary>
    public UserIalLevel IalLevel { get; set; } = UserIalLevel.None;

    /// <summary>
    /// The session ID from the ID proofing provider (e.g., Socure).
    /// </summary>
    public string? IdProofingSessionId { get; set; }

    /// <summary>
    /// The date and time when ID proofing was completed.
    /// </summary>
    public DateTime? IdProofingCompletedAt { get; set; }

    /// <summary>
    /// The date and time when ID proofing expires (if applicable).
    /// </summary>
    public DateTime? IdProofingExpiresAt { get; set; }

    /// <summary>
    /// The date and time when the user was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The date and time when the user record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether the user's record was co-loaded from an external system.
    /// This value is populated from external systems via batch processes or database queries.
    /// </summary>
    public bool IsCoLoaded { get; set; }

    /// <summary>
    /// The date and time when the co-loaded status was last updated from the source system.
    /// </summary>
    public DateTime? CoLoadedLastUpdated { get; set; }

    /// <summary>
    /// Phone number when used as household identifier for a state.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// SNAP case/client ID when used as household identifier for a state.
    /// </summary>
    public string? SnapId { get; set; }

    /// <summary>
    /// TANF case/client ID when used as household identifier for a state.
    /// </summary>
    public string? TanfId { get; set; }

    /// <summary>
    /// SSN or last-4 when used as household identifier for a state (per state policy).
    /// </summary>
    public string? Ssn { get; set; }

    /// <summary>
    /// Number of times this user has submitted ID proofing to Socure.
    /// Used to enforce the retry cap (max 3 attempts).
    /// </summary>
    public int IdProofingAttemptCount { get; set; }
}
