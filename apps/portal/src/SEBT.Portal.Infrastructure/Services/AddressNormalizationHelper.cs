using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Infrastructure.Services;

internal static class AddressNormalizationHelper
{
    internal static Address TrimToAddress(
        string street1,
        string? street2,
        string city,
        string state,
        string postalCode)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        return new Address
        {
            StreetAddress1 = T(street1) ?? string.Empty,
            StreetAddress2 = T(street2),
            City = T(city) ?? string.Empty,
            State = T(state) ?? string.Empty,
            PostalCode = FormatPostalCode(T(postalCode) ?? string.Empty)
        };
    }

    /// <summary>
    /// Normalizes ZIP to 5 digits or ZIP+4 with hyphen when both parts are present.
    /// </summary>
    internal static string FormatPostalCode(string postalCode)
    {
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());
        if (digits.Length >= 9)
        {
            return $"{digits.AsSpan(0, 5)}-{digits.AsSpan(5, 4)}";
        }

        if (digits.Length >= 5)
        {
            return digits[..5];
        }

        return postalCode.Trim();
    }

    internal static void SplitPostalCode(string postalCode, out string zip5, out string? plus4)
    {
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());
        if (digits.Length >= 9)
        {
            zip5 = digits[..5];
            plus4 = digits.Substring(5, 4);
            return;
        }

        if (digits.Length >= 5)
        {
            zip5 = digits[..5];
            plus4 = null;
            return;
        }

        zip5 = postalCode.Trim();
        plus4 = null;
    }

    internal static bool AddressesEqualLoose(Address a, Address b) =>
        string.Equals(NormalizeLine(a.StreetAddress1), NormalizeLine(b.StreetAddress1), StringComparison.OrdinalIgnoreCase)
        && string.Equals(NormalizeLine(a.StreetAddress2), NormalizeLine(b.StreetAddress2), StringComparison.OrdinalIgnoreCase)
        && string.Equals(NormalizeLine(a.City), NormalizeLine(b.City), StringComparison.OrdinalIgnoreCase)
        && string.Equals(NormalizeLine(a.State), NormalizeLine(b.State), StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            DigitsOnly(a.PostalCode),
            DigitsOnly(b.PostalCode),
            StringComparison.Ordinal);

    private static string? NormalizeLine(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string DigitsOnly(string? s) =>
        s == null ? string.Empty : new string(s.Where(char.IsDigit).ToArray());
}
