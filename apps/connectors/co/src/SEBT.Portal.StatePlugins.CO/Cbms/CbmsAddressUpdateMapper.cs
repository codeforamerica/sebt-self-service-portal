using System.Globalization;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using HouseholdAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Maps portal <see cref="HouseholdAddress"/> and account context into CBMS <see cref="UpdateStudentDetailsRequest"/>.
/// </summary>
internal static class CbmsAddressUpdateMapper
{
    /// <summary>
    /// Builds a CBMS PATCH body: required <c>addr</c> plus identifiers from the student row returned by get-account-details.
    /// </summary>
    /// <param name="resolvedSebtChldId">
    /// Optional id from <see cref="CbmsGetAccountStudentDetailIds.Resolve"/> when CBMS used non-standard JSON keys.
    /// </param>
    /// <param name="resolvedSebtAppId">Optional resolved application id (same).</param>
    public static UpdateStudentDetailsRequest ToUpdateStudentDetailsRequest(
        HouseholdAddress portalAddress,
        GetAccountStudentDetail studentRow,
        string? resolvedSebtChldId = null,
        string? resolvedSebtAppId = null)
    {
        ArgumentNullException.ThrowIfNull(portalAddress);
        ArgumentNullException.ThrowIfNull(studentRow);

        return new UpdateStudentDetailsRequest
        {
            SebtChldId = resolvedSebtChldId ?? studentRow.SebtChldId?.ToString(CultureInfo.InvariantCulture),
            SebtAppId = resolvedSebtAppId ?? studentRow.SebtAppId?.ToString(CultureInfo.InvariantCulture),
            Addr = ToCbmsAddress(portalAddress)
        };
    }

    /// <summary>
    /// Maps portal address fields to CBMS <c>addr</c> (addrLn1, addrLn2, city, state, zip, zip4).
    /// </summary>
    public static Address ToCbmsAddress(HouseholdAddress portalAddress)
    {
        ArgumentNullException.ThrowIfNull(portalAddress);

        var (zip, zip4) = SplitPostalCode(portalAddress.PostalCode);
        return new Address
        {
            AddrLn1 = portalAddress.StreetAddress1,
            AddrLn2 = portalAddress.StreetAddress2,
            Cty = portalAddress.City,
            StaCd = portalAddress.State,
            Zip = zip,
            Zip4 = zip4
        };
    }

    /// <summary>
    /// Validates that the portal supplied enough address data for CBMS (required addr in spec).
    /// </summary>
    public static bool TryValidatePortalAddress(HouseholdAddress address, out string? error)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (string.IsNullOrWhiteSpace(address.StreetAddress1))
        {
            error = "StreetAddress1 is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(address.City))
        {
            error = "City is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(address.State))
        {
            error = "State is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(address.PostalCode))
        {
            error = "PostalCode is required.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Mutates a cached <see cref="GetAccountStudentDetail"/> row to reflect the address that was
    /// just successfully PATCHed to CBMS, so that subsequent reads from the household cache
    /// return the post-update state without a round-trip.
    /// Uses the same field mapping as <see cref="ToCbmsAddress"/> so the cache stays consistent
    /// with what CBMS received.
    /// </summary>
    internal static void ApplyAddressToRow(HouseholdAddress address, GetAccountStudentDetail row)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(row);

        var (zip, zip4) = SplitPostalCode(address.PostalCode);
        row.AddrLn1 = address.StreetAddress1;
        row.AddrLn2 = address.StreetAddress2 ?? string.Empty;
        row.Cty = address.City;
        row.StaCd = address.State;
        row.Zip = zip;
        row.Zip4 = zip4 ?? string.Empty;
    }

    internal static (string? Zip, string? Zip4) SplitPostalCode(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return (null, null);

        var trimmed = postalCode.Trim();
        var dash = trimmed.IndexOf('-');
        if (dash >= 0)
        {
            var zip = trimmed[..dash].Trim();
            var zip4 = trimmed[(dash + 1)..].Trim();
            return (string.IsNullOrEmpty(zip) ? null : zip, string.IsNullOrEmpty(zip4) ? null : zip4);
        }

        return (trimmed, null);
    }
}
