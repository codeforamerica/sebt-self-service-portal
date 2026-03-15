using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Command to start a document verification challenge.
/// Generates a Socure DocV session token just-in-time for document capture.
/// </summary>
public class StartChallengeCommand : ICommand<StartChallengeResponse>
{
    /// <summary>
    /// The public GUID of the challenge to start.
    /// </summary>
    [Required(ErrorMessage = "ChallengeId is required.")]
    public Guid ChallengeId { get; init; }

    /// <summary>
    /// The authenticated user's internal ID. Used to enforce ownership.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer.")]
    public int UserId { get; init; }
}
