using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Applies a <see cref="PiiVisibility"/> to a <see cref="HouseholdData"/>,
/// masking fields the user is not authorized to see. Exposed as a shared
/// utility so the use-case layer can re-apply visibility once it has loaded
/// the household and can resolve per-case-type requirements against real cases.
/// </summary>
public static class HouseholdPiiFilter
{
    public static HouseholdData Apply(HouseholdData source, PiiVisibility piiVisibility)
    {
        return source with
        {
            Email = piiVisibility.IncludeEmail ? source.Email : PiiMasker.MaskEmail(source.Email),
            Phone = piiVisibility.IncludePhone ? source.Phone : PiiMasker.MaskPhone(source.Phone),
            AddressOnFile = piiVisibility.IncludeAddress && source.AddressOnFile != null
                ? new Address
                {
                    StreetAddress1 = source.AddressOnFile.StreetAddress1,
                    StreetAddress2 = source.AddressOnFile.StreetAddress2,
                    City = source.AddressOnFile.City,
                    State = source.AddressOnFile.State,
                    PostalCode = source.AddressOnFile.PostalCode
                }
                : source.AddressOnFile != null
                    ? new Address
                    {
                        StreetAddress1 = PiiMasker.MaskStreetAddress(
                            source.AddressOnFile.StreetAddress1,
                            source.AddressOnFile.StreetAddress2),
                        City = source.AddressOnFile.City,
                        State = source.AddressOnFile.State,
                        PostalCode = source.AddressOnFile.PostalCode
                    }
                    : null
        };
    }
}
