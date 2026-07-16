using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Models.AddressUpdate;

/// <summary>
/// Successful outcome of <see cref="SEBT.Portal.Core.Services.IAddressUpdateService"/>.
/// </summary>
public sealed record AddressUpdateSuccess
{
    /// <summary>USPS-style normalized mailing address.</summary>
    public required Address NormalizedAddress { get; init; }

    /// <summary>True when Smarty indicates USPS General Delivery (record type G or equivalent).</summary>
    public bool IsGeneralDelivery { get; init; }

    /// <summary>True when normalized fields differ from the submitted input (casing, abbreviations, ZIP+4).</summary>
    public bool WasCorrected { get; init; }
}
