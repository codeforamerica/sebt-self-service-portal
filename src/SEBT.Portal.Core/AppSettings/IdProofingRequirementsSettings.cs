namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for state-specific ID proofing requirements for PII data elements.
/// Each PII type (Address, Email, Phone etc.) can require a minimum assurance level.
/// "None" = no requirement; "IAL1" = user must have completed ID proofing (IAL1+).
/// </summary>
public class IdProofingRequirementsSettings
{
    public static readonly string SectionName = "IdProofingRequirements";

    /// <summary>
    /// Minimum assurance level required to view address. "None" or "IAL1".
    /// </summary>
    public string Address { get; set; } = "IAL1";

    /// <summary>
    /// Minimum assurance level required to view email. "None" or "IAL1".
    /// </summary>
    public string Email { get; set; } = "None";

    /// <summary>
    /// Minimum assurance level required to view phone. "None" or "IAL1".
    /// </summary>
    public string Phone { get; set; } = "None";
}
