using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Validates addresses against per-state blocked address lists, applies DC street
/// abbreviations for addresses exceeding 30 characters, and delegates to an external
/// validation service (e.g., Smarty) when available.
/// </summary>
public class AddressValidationService : IAddressValidationService
{
    private const int MaxStreetAddressLength = 30;

    private readonly string _state;
    private readonly HashSet<string> _blockedAddresses;

    /// <summary>
    /// Per-state blocked address lists. These are addresses where the state cannot
    /// deliver mail (e.g., government office buildings used as default addresses).
    /// </summary>
    private static readonly Dictionary<string, string[]> BlockedAddressesByState = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dc"] =
        [
            "2100 Martin Luther King Jr Avenue SE",
            "3851 Alabama Avenue SE",
            "4049 South Capitol Street SW",
            "645 H Street NE",
            "1207 Taylor Street NW"
        ],
        ["co"] =
        [
            "1575 Sherman St"
        ]
    };

    /// <summary>
    /// DC street name abbreviations for addresses that exceed 30 characters.
    /// The card vendor (FIS) has a 30-character limit on address line 1.
    /// These mappings come from the state's known long street names.
    /// </summary>
    private static readonly Dictionary<string, string> DcStreetAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALBERT IRVIN CASSELL"] = "ALBERT IRVIN CASS",
        ["COMMODORE JOSHUA BARNEY"] = "COMMODORE JOSH BARN",
        ["MARTIN LUTHER KING JR"] = "MLK JR",
        ["NANNIE HELEN BURROUGHS"] = "N H BURROUGHS",
        ["PATRICIA ROBERTS HARRIS"] = "PATRICIA RBRTS HARR",
        ["ROBERT CLIFTON WEAVER"] = "ROBRT CLIFTN WEAVR"
    };

    public AddressValidationService(string state)
    {
        _state = state;
        var addresses = BlockedAddressesByState.GetValueOrDefault(state, []);
        _blockedAddresses = new HashSet<string>(
            addresses.Select(NormalizeStreet),
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<AddressValidationResult> ValidateAsync(Address address, CancellationToken cancellationToken = default)
    {
        if (IsBlocked(address))
        {
            return Task.FromResult(
                AddressValidationResult.Invalid("This address cannot be used for mail delivery.", "blocked"));
        }

        if (IsDc && address.StreetAddress1?.Length > MaxStreetAddressLength)
        {
            var abbreviated = TryAbbreviateStreet(address.StreetAddress1);
            if (abbreviated != null)
            {
                var suggested = new Address
                {
                    StreetAddress1 = abbreviated,
                    StreetAddress2 = address.StreetAddress2,
                    City = address.City,
                    State = address.State,
                    PostalCode = address.PostalCode
                };
                return Task.FromResult(AddressValidationResult.Suggestion(suggested, "abbreviated"));
            }

            return Task.FromResult(
                AddressValidationResult.Invalid("Enter a street address shorter than 30 characters.", "too_long"));
        }

        return Task.FromResult(AddressValidationResult.Valid());
    }

    private bool IsDc => string.Equals(_state, "dc", StringComparison.OrdinalIgnoreCase);

    private bool IsBlocked(Address address)
    {
        if (string.IsNullOrWhiteSpace(address.StreetAddress1))
        {
            return false;
        }

        var normalized = NormalizeStreet(address.StreetAddress1);
        return _blockedAddresses.Contains(normalized);
    }

    /// <summary>
    /// Attempts to shorten a DC street address by replacing a known long street name
    /// with its abbreviated form. Returns null if no abbreviation applies or if the
    /// result still exceeds the character limit.
    /// </summary>
    private static string? TryAbbreviateStreet(string streetAddress)
    {
        foreach (var (full, abbreviated) in DcStreetAbbreviations)
        {
            var index = streetAddress.IndexOf(full, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;

            var result = string.Concat(
                streetAddress.AsSpan(0, index),
                abbreviated,
                streetAddress.AsSpan(index + full.Length));

            if (result.Length <= MaxStreetAddressLength)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Strips punctuation, collapses whitespace, and trims for comparison.
    /// </summary>
    private static string NormalizeStreet(string street)
    {
        var cleaned = street
            .Replace(",", "")
            .Replace(".", "");

        // Collapse multiple spaces into one
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        return cleaned.Trim();
    }
}
