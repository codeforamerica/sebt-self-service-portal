namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Entity model for tracking user IAL level.
/// </summary>
public class UserEntity
{
    /// <summary>
    /// The unique identifier for the user (primary key).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user's email address, used as a unique identifier.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Workflow state of ID proofing (NotStarted, InProgress, Completed, Failed, Expired)
    /// </summary>
    public int IdProofingStatus { get; set; } = 0; // 0 = NotStarted

    /// <summary>
    /// The Identity Assurance Level (IAL) this user has achieved. Stored as int for enum
    /// valid values: 0 = None, 1 = IAL1, 2 = IAL1plus, 3 = IAL2
    /// </summary>
    public int IalLevel { get; set; } = 0; // 0 = UserIalLevel.None

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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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

    /// <summary>Phone number when used as household identifier.</summary>
    public string? Phone { get; set; }

    /// <summary>SNAP case/client ID when used as household identifier.</summary>
    public string? SnapId { get; set; }

    /// <summary>TANF case/client ID when used as household identifier.</summary>
    public string? TanfId { get; set; }

    /// <summary>SSN or last-4 when used as household identifier (per state policy).</summary>
    public string? Ssn { get; set; }
}
