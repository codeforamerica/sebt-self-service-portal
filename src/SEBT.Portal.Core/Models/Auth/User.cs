namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Represents a user in the system with ID proofing status.
/// </summary>
public class User
{
    /// <summary>
    /// The user's email address, used as the unique identifier.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// The current ID proofing status for this user.
    /// </summary>
    public IdProofingStatus IdProofingStatus { get; set; } = IdProofingStatus.NotStarted;

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
}
