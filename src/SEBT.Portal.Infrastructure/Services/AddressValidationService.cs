using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Validates addresses against configurable blocked address lists and applies street name
/// abbreviations for addresses exceeding a configurable length limit.
/// Runs after Smarty normalization (handled by <c>UpdateAddressCommandHandler</c>)
/// so that checks operate on the canonical address form.
///
/// All state-specific data (blocked addresses, abbreviation mappings, length limits)
/// is loaded from <see cref="AddressValidationDataSettings"/> via appsettings configuration.
/// </summary>
public class AddressValidationService : IAddressValidationService
{
    private readonly HashSet<string> _blockedAddresses;
    private readonly Dictionary<string, string> _streetAbbreviations;
    private readonly int _maxStreetAddressLength;

    /// <summary>
    /// Common USPS street type abbreviations mapped to their canonical full forms.
    /// Used by NormalizeStreet to ensure consistent comparison regardless of whether
    /// the user enters "St" or "Street", "Ave" or "Avenue", etc.
    /// Note: "St" is treated as "Street" (not "Saint"). No current blocked address
    /// contains a "Saint" street name, so this does not cause false positives.
    /// </summary>
    private static readonly Dictionary<string, string> StreetTypeExpansions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["St"] = "Street",
        ["Ave"] = "Avenue",
        ["Blvd"] = "Boulevard",
        ["Dr"] = "Drive",
        ["Ln"] = "Lane",
        ["Ct"] = "Court",
        ["Pl"] = "Place",
        ["Rd"] = "Road",
        ["Cir"] = "Circle",
        ["Ter"] = "Terrace",
        ["Pkwy"] = "Parkway",
        ["Hwy"] = "Highway"
    };

    public AddressValidationService(IOptions<AddressValidationDataSettings> options)
    {
        var settings = options.Value;

        _blockedAddresses = new HashSet<string>(
            settings.BlockedAddresses.Select(NormalizeStreet),
            StringComparer.OrdinalIgnoreCase);

        _streetAbbreviations = new Dictionary<string, string>(
            settings.StreetAbbreviations,
            StringComparer.OrdinalIgnoreCase);

        _maxStreetAddressLength = settings.MaxStreetAddressLength;
    }

    public Task<AddressValidationResult> ValidateAsync(Address address, CancellationToken cancellationToken = default)
    {
        if (IsBlocked(address))
        {
            return Task.FromResult(
                AddressValidationResult.Invalid("This address cannot be used for mail delivery.", "blocked"));
        }

        if (_maxStreetAddressLength > 0 && address.StreetAddress1?.Length > _maxStreetAddressLength)
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
    /// Attempts to shorten a street address by replacing a known long street name
    /// with its abbreviated form. Returns null if no abbreviation applies or if the
    /// result still exceeds the character limit.
    /// </summary>
    private string? TryAbbreviateStreet(string streetAddress)
    {
        foreach (var (full, abbreviated) in _streetAbbreviations)
        {
            var index = streetAddress.IndexOf(full, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;

            var result = string.Concat(
                streetAddress.AsSpan(0, index),
                abbreviated,
                streetAddress.AsSpan(index + full.Length));

            if (result.Length <= _maxStreetAddressLength)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Strips punctuation, expands street type abbreviations, collapses whitespace,
    /// and trims for consistent comparison. Both blocked list entries and user input
    /// pass through this method, so expanding abbreviations makes "645 H St NE"
    /// match "645 H Street NE" regardless of which form was entered.
    /// </summary>
    private static string NormalizeStreet(string street)
    {
        var cleaned = street
            .Replace(",", "")
            .Replace(".", "");

        // Expand street type abbreviations word by word to avoid partial-word
        // mangling (e.g., "Stanton" must NOT become "Streetanton")
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (StreetTypeExpansions.TryGetValue(words[i], out var expanded))
            {
                words[i] = expanded;
            }
        }

        return string.Join(' ', words);
    }
}
