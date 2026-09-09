using PhoneNumbers;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Normalizes phone numbers to 10-digit US national format for CBMS API lookups.
/// Uses libphonenumber for robust parsing and validation.
/// </summary>
internal static class PhoneNormalizer
{
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    /// <summary>
    /// Parses and validates a phone number as a US number, returning the 10-digit
    /// national number (e.g. "8185551234") or null if invalid.
    /// </summary>
    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var number = PhoneUtil.Parse(value, "US");

            if (!PhoneUtil.IsValidNumberForRegion(number, "US"))
                return null;

            return number.NationalNumber.ToString();
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
