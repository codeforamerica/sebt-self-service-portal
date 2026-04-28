using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Command to retry document verification after a Socure RESUBMIT decision (DC-301).
/// Opens a brand-new <c>docv_stepup</c> evaluation; the existing Resubmit challenge is left
/// untouched and a fresh <c>DocVerificationChallenge</c> row is persisted in Pending.
/// </summary>
public class ResubmitChallengeCommand : ICommand<ResubmitChallengeResponse>
{
    /// <summary>
    /// The public GUID of the prior challenge (must be in Resubmit state).
    /// </summary>
    public Guid ChallengeId { get; init; }

    /// <summary>
    /// The authenticated user's internal ID. Used to enforce ownership.
    /// Guaranteed non-empty by ResolveUserFilter before the command is built.
    /// </summary>
    public Guid UserId { get; init; }
}
