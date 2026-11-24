namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Controls how household data is looked up for co-loaded (SNAP/TANF) users whose login email
/// does not match a household in the state backend.
/// </summary>
public class CoLoadedHouseholdLookupSettings : IHaveConfigSectionName
{
    public static string SectionName => "CoLoadedHouseholdLookup";

    /// <summary>
    /// When <c>true</c>, a co-loaded user whose email lookup finds no household is retried by their
    /// SNAP/TANF identifier and date of birth. Enable only for state backends that support that match.
    /// </summary>
    public bool FallbackToBenefitIdAndDob { get; set; }
}
