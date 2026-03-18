namespace SEBT.Portal.Core.Models.DocVerification;

/// <summary>
/// Lifecycle states for a document verification challenge.
/// Transitions: Created → Pending → Verified | Rejected | Expired.
/// Terminal states (Verified, Rejected, Expired) cannot be overwritten.
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
    Expired = 4
}
