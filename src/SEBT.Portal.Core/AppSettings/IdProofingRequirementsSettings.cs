namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for state-specific ID proofing requirements for PII data elements.
/// Each PII type (Address, Email, Phone etc.) can require a minimum assurance level.
/// Valid values: "IAL1", "IAL1plus", "IAL2".
/// </summary>
public class IdProofingRequirementsSettings
{
    public static readonly string SectionName = "IdProofingRequirements";

    /// <summary>
    /// Minimum assurance level required to view address. Valid: IAL1, IAL1plus, IAL2.
    /// </summary>
    public string Address { get; set; } = "IAL1plus";

    /// <summary>
    /// Minimum assurance level required to view email. Valid: IAL1, IAL1plus, IAL2.
    /// </summary>
    public string Email { get; set; } = "IAL1";

    /// <summary>
    /// Minimum assurance level required to view phone. Valid: IAL1, IAL1plus, IAL2.
    /// </summary>
    public string Phone { get; set; } = "IAL1";
}
