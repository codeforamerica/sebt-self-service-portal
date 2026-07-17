namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Identity Assurance Level (IAL) for ID proofing requirements.
/// Config values are bound case-insensitively (e.g. "IAL1", "ial1", "IAL1plus").
/// </summary>
public enum IalLevel
{
    /// <summary>
    /// User must have completed ID proofing.
    /// </summary>
    IAL1 = 0,

    /// <summary>
    /// User must meet IAL1+.
    /// </summary>
    IAL1plus = 1,

    /// <summary>
    /// Placeholder for future use: IAL2 not yet supported; This would supercede IAL1plus.
    /// </summary>
    IAL2 = 2
}
