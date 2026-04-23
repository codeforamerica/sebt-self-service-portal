using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Query to check the current status of a document verification challenge.
/// Polled by the frontend with exponential backoff after document capture.
/// </summary>
public class GetVerificationStatusQuery : IQuery<VerificationStatusResponse>
{
    /// <summary>
    /// The public GUID of the challenge to check.
    /// </summary>
    public Guid ChallengeId { get; init; }

    /// <summary>
    /// The authenticated user's internal ID. Used to enforce ownership.
    /// Guaranteed non-empty by ResolveUserFilter before the query is built.
    /// </summary>
    public Guid UserId { get; init; }
}
