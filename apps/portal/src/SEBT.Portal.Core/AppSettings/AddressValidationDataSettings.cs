namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Per-state address validation data loaded from <c>appsettings.{state}.json</c>.
/// Contains blocked address lists, street abbreviation mappings, and length constraints
/// that vary by state. Empty defaults allow states without these constraints to run
/// without additional configuration.
/// </summary>
public sealed class AddressValidationDataSettings
{
    public const string SectionName = "AddressValidationData";

    /// <summary>
    /// Addresses where the state cannot deliver mail (e.g., government office buildings
    /// used as default addresses). Matched after street type normalization, so both
    /// "645 H St NE" and "645 H Street NE" will match.
    /// </summary>
    public string[] BlockedAddresses { get; set; } = [];

    /// <summary>
    /// Maps long street names to abbreviated forms for addresses that exceed
    /// <see cref="MaxStreetAddressLength"/>. When a match is found and the abbreviated
    /// result fits within the limit, the service returns a suggestion instead of rejecting.
    /// </summary>
    public Dictionary<string, string> StreetAbbreviations { get; set; } = new();

    /// <summary>
    /// Maximum allowed length for street address line 1, enforced by the card vendor.
    /// Set to 0 to disable the length check (default). When set, addresses exceeding
    /// this limit trigger abbreviation lookup or rejection.
    /// </summary>
    public int MaxStreetAddressLength { get; set; }
}
