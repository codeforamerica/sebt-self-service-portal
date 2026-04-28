namespace SEBT.Portal.Core.Models.DocVerification;

/// <summary>
/// Represents a single document verification attempt for a user.
/// Each challenge tracks its own lifecycle independently from the user's overall proofing status.
/// The user entity is only updated when a challenge reaches a terminal state.
/// </summary>
public class DocVerificationChallenge
{
    /// <summary>
    /// Database primary key. Not exposed in API responses — use <see cref="PublicId"/> instead.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Opaque identifier exposed to API consumers. Prevents IDOR enumeration of challenge records.
    /// </summary>
    public Guid PublicId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The ID of the user who owns this challenge.
    /// All API reads are scoped by (PublicId, UserId) to enforce ownership.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Current lifecycle state. Transitions enforced by <see cref="TransitionTo"/>.
    /// </summary>
    public DocVerificationStatus Status { get; private set; } = DocVerificationStatus.Created;

    /// <summary>
    /// Socure's reference ID, used as the primary key for webhook correlation.
    /// Set when the DocV session is started (transition to Pending).
    /// </summary>
    public string? SocureReferenceId { get; set; }

    /// <summary>
    /// Socure's evaluation ID, used as a fallback key for webhook correlation.
    /// </summary>
    public string? EvalId { get; set; }

    /// <summary>
    /// The event ID from the last processed Socure webhook. Used for idempotency —
    /// if a webhook arrives with an event_id already recorded, it is silently ignored.
    /// </summary>
    public string? SocureEventId { get; set; }

    /// <summary>
    /// The Socure DocV transaction token passed to the frontend SDK.
    /// Set when the DocV session is started.
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
    /// Whether the user is allowed to retry with a different ID number instead of document verification.
    /// Derived server-side based on compliance policy — never passed via URL.
    /// </summary>
    public bool AllowIdRetry { get; set; } = true;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this challenge expires. Pending challenges past this time are transitioned to Expired on read.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Date of birth (yyyy-MM-dd) from the ID-proofing submission that created this challenge.
    /// Used to re-run Socure evaluation when the DocV transaction token expires.
    /// </summary>
    public string? ProofingDateOfBirth { get; set; }

    /// <summary>
    /// Government ID type from the ID-proofing submission (e.g. ssn, itin).
    /// </summary>
    public string? ProofingIdType { get; set; }

    /// <summary>
    /// Government ID value from the ID-proofing submission.
    /// </summary>
    public string? ProofingIdValue { get; set; }

    /// <summary>
    /// When <see cref="DocvTransactionToken"/> was last issued or refreshed (UTC).
    /// </summary>
    public DateTime? DocvTokenIssuedAt { get; set; }

    /// <summary>
    /// Whether this challenge is in a terminal state (Verified, Rejected, Expired, or Resubmit).
    /// Terminal challenges cannot be modified. A Resubmit challenge is terminal at this challenge's
    /// scope; the user opens a fresh challenge to retry.
    /// </summary>
    public bool IsTerminal => Status is DocVerificationStatus.Verified
        or DocVerificationStatus.Rejected
        or DocVerificationStatus.Expired
        or DocVerificationStatus.Resubmit;

    /// <summary>
    /// Transitions the challenge to a new status, enforcing valid state transitions.
    /// Allowed: Created → Pending → Verified | Rejected | Expired | Resubmit.
    /// Terminal states cannot be overwritten.
    /// </summary>
    /// <param name="newStatus">The target status.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the transition is not allowed from the current state.
    /// </exception>
    public void TransitionTo(DocVerificationStatus newStatus)
    {
        if (!IsValidTransition(newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {newStatus}.");
        }

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        // Scrub id-proofing PII once the challenge reaches a terminal state. These fields
        // exist only to re-issue a Socure DocV token while the challenge is still active;
        // once terminal they serve no further purpose and should not sit at rest. Resubmit
        // is included: any retry opens a brand-new challenge with freshly minted tokens.
        // Keep DocvTokenIssuedAt — it is a timestamp, not PII, and is useful for auditing
        // how long the last-issued token lived before the terminal transition.
        if (IsTerminal)
        {
            ProofingDateOfBirth = null;
            ProofingIdType = null;
            ProofingIdValue = null;
        }
    }

    private bool IsValidTransition(DocVerificationStatus newStatus) =>
        (Status, newStatus) switch
        {
            (DocVerificationStatus.Created, DocVerificationStatus.Pending) => true,
            (DocVerificationStatus.Pending, DocVerificationStatus.Verified) => true,
            (DocVerificationStatus.Pending, DocVerificationStatus.Rejected) => true,
            (DocVerificationStatus.Pending, DocVerificationStatus.Expired) => true,
            (DocVerificationStatus.Pending, DocVerificationStatus.Resubmit) => true,
            // Created can also expire directly if the user never starts
            (DocVerificationStatus.Created, DocVerificationStatus.Expired) => true,
            _ => false
        };

    /// <summary>
    /// Reconstitutes a challenge from persisted data without replaying state transitions.
    /// Used by the repository layer during deserialization. Business logic should use
    /// <see cref="TransitionTo"/> instead.
    /// </summary>
    public static DocVerificationChallenge Reconstitute(
        Guid id,
        Guid publicId,
        Guid userId,
        DocVerificationStatus status,
        string? socureReferenceId,
        string? evalId,
        string? socureEventId,
        string? docvTransactionToken,
        string? docvUrl,
        string? offboardingReason,
        bool allowIdRetry,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime? expiresAt,
        string? proofingDateOfBirth = null,
        string? proofingIdType = null,
        string? proofingIdValue = null,
        DateTime? docvTokenIssuedAt = null)
    {
        var challenge = new DocVerificationChallenge
        {
            Id = id,
            PublicId = publicId,
            UserId = userId,
            SocureReferenceId = socureReferenceId,
            EvalId = evalId,
            SocureEventId = socureEventId,
            DocvTransactionToken = docvTransactionToken,
            DocvUrl = docvUrl,
            OffboardingReason = offboardingReason,
            AllowIdRetry = allowIdRetry,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ExpiresAt = expiresAt,
            ProofingDateOfBirth = proofingDateOfBirth,
            ProofingIdType = proofingIdType,
            ProofingIdValue = proofingIdValue,
            DocvTokenIssuedAt = docvTokenIssuedAt
        };

        // Set status directly — bypasses TransitionTo because this is reconstitution,
        // not a business state change. The data was validated when it was originally written.
        challenge.Status = status;

        return challenge;
    }
}
