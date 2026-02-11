namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// The Identity Assurance Level (IAL) a user has achieved through ID proofing.
/// Used for PII visibility and feature access decisions.
/// </summary>
public enum UserIalLevel
{
    /// <summary>
    /// No IAL achieved. User has not completed ID proofing, or proofing is in progress, failed, or expired.
    /// </summary>
    None = 0,

    /// <summary>
    /// Basic identity verification completed (IAL1).
    /// </summary>
    IAL1 = 1,

    /// <summary>
    /// Enhanced verification completed (IAL1+).
    /// </summary>
    IAL1plus = 2,

    /// <summary>
    /// IAL2 not yet supported. Reserved for future use.
    /// </summary>
    IAL2 = 3
}
