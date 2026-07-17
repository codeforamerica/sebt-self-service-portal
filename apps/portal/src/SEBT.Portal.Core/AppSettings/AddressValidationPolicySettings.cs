namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Per-state policy for address updates. Override in <c>appsettings.{state}.json</c> as needed.
/// </summary>
public sealed class AddressValidationPolicySettings
{
    public const string SectionName = "AddressValidationPolicy";

    /// <summary>
    /// When true, USPS General Delivery addresses validated via Smarty are accepted.
    /// When false, General Delivery is rejected with a structured validation error.
    /// </summary>
    public bool AllowGeneralDelivery { get; set; } = true;
}
