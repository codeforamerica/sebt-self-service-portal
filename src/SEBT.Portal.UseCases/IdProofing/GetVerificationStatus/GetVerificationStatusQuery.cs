using System.ComponentModel.DataAnnotations;
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
    [Required(ErrorMessage = "ChallengeId is required.")]
    public Guid ChallengeId { get; init; }

    /// <summary>
    /// The authenticated user's internal ID. Used to enforce ownership.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer.")]
    public int UserId { get; init; }
}
