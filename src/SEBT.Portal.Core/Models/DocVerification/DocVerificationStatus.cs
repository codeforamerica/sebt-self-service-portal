namespace SEBT.Portal.Core.Models.DocVerification;

/// <summary>
/// Lifecycle states for a document verification challenge.
/// Transitions: Created → Pending → Verified | Rejected | Expired | Resubmit.
/// All four post-Pending states are terminal and cannot be overwritten. Resubmit is terminal at
/// the Socure level (the workflow ended) but retry-eligible at the portal level: a user who
/// lands on Resubmit can open a fresh challenge against Socure's `docv_stepup` workflow.
/// </summary>
public enum DocVerificationStatus
{
    /// <summary>
    /// Challenge has been created but the user has not yet started the Socure DocV flow.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The user has started the DocV flow and Socure is awaiting document capture/evaluation.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Socure accepted the user's identity documents. Terminal state.
    /// </summary>
    Verified = 2,

    /// <summary>
    /// Socure rejected the user's identity documents. Terminal state.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// The challenge expired before the user completed the flow. Terminal state.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Socure returned a RESUBMIT decision (recoverable failure such as image quality).
    /// Terminal at Socure: the workflow ended and a retry must start a brand-new evaluation.
    /// Retry-eligible at the portal: a fresh `DocVerificationChallenge` can be opened against
    /// Socure's `docv_stepup` workflow when the user clicks "try again".
    /// </summary>
    Resubmit = 5
}
