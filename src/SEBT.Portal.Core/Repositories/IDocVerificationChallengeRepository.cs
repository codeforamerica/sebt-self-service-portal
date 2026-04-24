using SEBT.Portal.Core.Models.DocVerification;

namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Repository for managing document verification challenge records.
/// All read operations are scoped by userId to enforce ownership.
/// </summary>
public interface IDocVerificationChallengeRepository
{
    /// <summary>
    /// Retrieves a challenge by its public ID and owning user ID.
    /// Returns null if the challenge does not exist or belongs to a different user.
    /// </summary>
    Task<DocVerificationChallenge?> GetByPublicIdAsync(
        Guid publicId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active (non-terminal) challenge for a user, if one exists.
    /// Used to enforce the one-active-challenge-per-user constraint.
    /// </summary>
    Task<DocVerificationChallenge?> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a challenge by its Socure reference ID for webhook correlation.
    /// Falls back to EvalId if referenceId lookup returns null.
    /// </summary>
    Task<DocVerificationChallenge?> GetBySocureReferenceIdAsync(
        string referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a challenge by its Socure evaluation ID (fallback correlation).
    /// </summary>
    Task<DocVerificationChallenge?> GetByEvalIdAsync(
        string evalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new challenge record.
    /// </summary>
    Task CreateAsync(
        DocVerificationChallenge challenge,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing challenge record.
    /// </summary>
    Task UpdateAsync(
        DocVerificationChallenge challenge,
        CancellationToken cancellationToken = default);
}
