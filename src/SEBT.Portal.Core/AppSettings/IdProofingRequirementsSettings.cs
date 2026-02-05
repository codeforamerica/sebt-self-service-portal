using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for state-specific ID proofing requirements for PII data elements.
/// Uses field-based + action keys (e.g. address+view) to support future view vs edit requirements.
/// Valid values: IAL1, IAL1plus, IAL2 (Case-insensitive).
/// </summary>
public class IdProofingRequirementsSettings
{
    public static readonly string SectionName = "IdProofingRequirements";

    /// <summary>
    /// Minimum assurance level required to view address. Valid: IAL1, IAL1plus, IAL2.
    /// </summary>
    [ConfigurationKeyName("address+view")]
    [DefaultValue(IalLevel.IAL1plus)]
    public IalLevel AddressView { get; set; } = IalLevel.IAL1plus;

    /// <summary>
    /// Minimum assurance level required to view email. Valid: IAL1, IAL1plus, IAL2.
    /// </summary>
    [ConfigurationKeyName("email+view")]
    [DefaultValue(IalLevel.IAL1)]
    public IalLevel EmailView { get; set; } = IalLevel.IAL1;

    /// <summary>
    /// Minimum assurance level required to view phone. Valid: IAL1, IAL1plus, IAL2.
    /// </summary>
    [ConfigurationKeyName("phone+view")]
    [DefaultValue(IalLevel.IAL1)]
    public IalLevel PhoneView { get; set; } = IalLevel.IAL1;
}
