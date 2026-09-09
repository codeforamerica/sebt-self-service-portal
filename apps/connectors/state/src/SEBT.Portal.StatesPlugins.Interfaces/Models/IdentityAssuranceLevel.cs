namespace SEBT.Portal.StatesPlugins.Interfaces.Models;

/// <summary>
/// Identity Assurance Level (IAL) the user has achieved through ID proofing.
/// Matches portal Core.UserIalLevel and aligns with NIST 800-63; used at the plugin boundary
/// for PII/backend policy (e.g. whether to return address).
/// </summary>
public enum IdentityAssuranceLevel
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
