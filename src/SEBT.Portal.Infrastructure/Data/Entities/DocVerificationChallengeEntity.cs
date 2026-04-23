using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Entity model for tracking individual document verification attempts.
/// Each challenge has its own lifecycle independent from the user's overall proofing status.
/// </summary>
public class DocVerificationChallengeEntity
{
    /// <summary>
    /// Database primary key. Not exposed in API responses.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Opaque identifier exposed to API consumers. Prevents IDOR enumeration.
    /// </summary>
    public Guid PublicId { get; set; }

    /// <summary>
    /// Foreign key to the owning user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Current lifecycle state (0=Created, 1=Pending, 2=Verified, 3=Rejected, 4=Expired).
    /// </summary>
    public int Status { get; set; } = 0; // Created

    /// <summary>
    /// Socure's reference ID for primary webhook correlation.
    /// </summary>
    public string? SocureReferenceId { get; set; }

    /// <summary>
    /// Socure's evaluation ID for fallback webhook correlation.
    /// </summary>
    public string? EvalId { get; set; }

    /// <summary>
    /// The event ID from the last processed Socure webhook. Used for idempotency.
    /// </summary>
    public string? SocureEventId { get; set; }

    /// <summary>
    /// The Socure DocV transaction token passed to the frontend SDK.
    /// </summary>
    public string? DocvTransactionToken { get; set; }

    /// <summary>
    /// The Socure verification URL for the document capture flow.
    /// </summary>
    public string? DocvUrl { get; set; }

    /// <summary>
    /// Reason code for off-boarding when verification is rejected.
    /// </summary>
    public string? OffboardingReason { get; set; }

    /// <summary>
    /// Whether the user can retry with a different ID number.
    /// </summary>
    public bool AllowIdRetry { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this challenge expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Date of birth from ID proofing (yyyy-MM-dd), for refreshing an expired DocV token.
    /// </summary>
    public string? ProofingDateOfBirth { get; set; }

    /// <summary>
    /// ID type from ID proofing (e.g. ssn, itin).
    /// </summary>
    public string? ProofingIdType { get; set; }

    /// <summary>
    /// ID value from ID proofing.
    /// </summary>
    public string? ProofingIdValue { get; set; }

    /// <summary>
    /// When the DocV transaction token was last issued (UTC).
    /// </summary>
    public DateTime? DocvTokenIssuedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. SQL Server auto-increments this on every UPDATE.
    /// EF Core uses it to detect concurrent modifications.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Navigation property to the owning user.
    /// </summary>
    public UserEntity? User { get; set; }
}
