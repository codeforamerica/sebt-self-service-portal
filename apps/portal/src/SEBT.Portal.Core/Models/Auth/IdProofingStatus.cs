namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Workflow state of ID proofing for a user.
/// Separate from IAL (Identity Assurance Level) - status tracks process state, IAL tracks assurance achieved.
/// </summary>
public enum IdProofingStatus
{
    /// <summary>
    /// ID proofing has not been initiated.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// ID proofing is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// ID proofing has been completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// ID proofing failed or was rejected.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// ID proofing has expired and needs to be renewed.
    /// </summary>
    Expired = 4
}
