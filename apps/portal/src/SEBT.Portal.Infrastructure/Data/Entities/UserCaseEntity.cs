namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// One Summer EBT case that was displayed to a portal user. Identifiers are stored encrypted
/// (for later retrieval) and as HMAC hashes (for joins and upserts).
/// </summary>
public class UserCaseEntity
{
    /// <summary>
    /// Primary key (UUIDv7, application-generated).
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The portal user who was shown this case.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public UserEntity? User { get; set; }

    /// <summary>
    /// AES-GCM ciphertext of <see cref="Core.Models.Household.SummerEbtCase.SummerEBTCaseID"/>.
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 of the case id. 64-character hex from <see cref="Core.Services.IIdentifierHasher"/>.
    /// </summary>
    public string CaseIdHash { get; set; } = string.Empty;

    /// <summary>
    /// AES-GCM ciphertext of the application id when the case is application-based.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// HMAC-SHA256 of the application id, or empty when the case has none.
    /// </summary>
    public string ApplicationIdHash { get; set; } = string.Empty;

    /// <summary>
    /// AES-GCM ciphertext of the per-child application student id when present.
    /// </summary>
    public string? ApplicationStudentId { get; set; }

    /// <summary>
    /// HMAC-SHA256 of the application student id, or empty when the case has none.
    /// </summary>
    public string ApplicationStudentIdHash { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this user was first shown this case.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when this user was most recently shown this case.
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
