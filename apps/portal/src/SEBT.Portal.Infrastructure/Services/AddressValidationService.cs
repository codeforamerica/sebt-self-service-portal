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
/// Two blocked-address paths participate:
/// 1. The legacy inline list from <see cref="AddressValidationDataSettings.BlockedAddresses"/>,
///    matched on normalized street alone (used for small hand-curated lists, e.g. DC's 5 entries).
/// 2. <see cref="IBlockedAddressDataSource"/> entries, matched on (normalized street + 5-digit ZIP)
///    so that large state-wide lists can disambiguate same-street collisions across cities.
/// </summary>
public class AddressValidationService : IAddressValidationService
{
    private readonly HashSet<string> _blockedStreetsLegacy;
    private readonly Dictionary<string, HashSet<string>> _blockedStreetsByZip;
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

    /// <summary>
    /// Standalone directional tokens that USPS / Smarty may add or omit.
    /// Stripped only on the ZIP-keyed matching path: ZIP context narrows the
    /// match enough that cross-directional collisions are negligible. The
    /// legacy L1-only path keeps directionals because DC street names depend
    /// on the quadrant suffix to disambiguate (e.g. Taylor St NW vs SW).
    /// </summary>
    private static readonly HashSet<string> Directionals = new(StringComparer.OrdinalIgnoreCase)
    {
        "N", "S", "E", "W", "NE", "NW", "SE", "SW",
        "North", "South", "East", "West",
        "Northeast", "Northwest", "Southeast", "Southwest"
    };

    /// <summary>
    /// USPS secondary unit designators (Pub 28 Appendix C2). Smarty's US Street
    /// API sometimes parses these into streetAddress2 and sometimes leaves them
    /// in streetAddress1; when they land in L1 they break exact-match against a
    /// CSV row that has no unit info. The loose matcher truncates from the first
    /// indicator onwards because USPS convention places unit data at the END of
    /// the street component.
    /// </summary>
    private static readonly HashSet<string> UnitIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ste", "Suite",
        "Apt", "Apartment",
        "Unit",
        "Fl", "Floor",
        "Rm", "Room",
        "Bldg", "Building",
        "Bsmt", "Basement",
        "Dept", "Department",
        "Lowr", "Lower",
        "Uppr", "Upper",
        "Ph", "Penthouse",
        "Trlr", "Trailer",
        "Hngr", "Hangar",
        "Ofc", "Office",
        "Spc", "Space",
        "Lot", "Slip", "Stop", "Pier", "Key",
        "Frnt", "Front", "Rear", "Side"
    };

    public AddressValidationService(
        IOptions<AddressValidationDataSettings> options,
        IBlockedAddressDataSource blockedAddressData)
    {
        var settings = options.Value;

        _blockedStreetsLegacy = new HashSet<string>(
            settings.BlockedAddresses.Select(NormalizeStreet),
            StringComparer.OrdinalIgnoreCase);

        _blockedStreetsByZip = blockedAddressData.GetEntries()
            .GroupBy(e => e.PostalCodeFive, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new HashSet<string>(
                    g.Select(e => NormalizeStreetLoose(e.Street)),
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal);

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

        if (_blockedStreetsLegacy.Contains(NormalizeStreet(address.StreetAddress1)))
        {
            return true;
        }

        var zip5 = TryGetZipFive(address.PostalCode);
        if (zip5 is not null
            && _blockedStreetsByZip.TryGetValue(zip5, out var streetsAtZip)
            && streetsAtZip.Contains(NormalizeStreetLoose(address.StreetAddress1)))
        {
            return true;
        }

        return false;
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

    /// <summary>
    /// Like NormalizeStreet but additionally drops standalone directional tokens
    /// and truncates from the first unit indicator. Used on the ZIP-keyed matching
    /// path to absorb Smarty's two main canonicalization drifts:
    ///
    /// 1. Directional drift: Smarty may add or omit "N"/"S"/"E"/"W" on streets
    ///    where it isn't required (e.g. CSV "1575 Sherman St" → Smarty's "1575 N Sherman St").
    /// 2. Unit drift: Smarty sometimes leaves "Ste 200"/"Apt 4B"/"Fl 7" in L1 rather
    ///    than splitting it into L2; the source CSV doesn't carry unit info in L1,
    ///    so an exact match would miss the building.
    ///
    /// ZIP context constrains both transformations enough that they're safe in practice.
    /// </summary>
    private static string NormalizeStreetLoose(string street)
    {
        var normalized = NormalizeStreet(street);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(words.Length);
        foreach (var word in words)
        {
            if (UnitIndicators.Contains(word))
            {
                break;
            }
            if (Directionals.Contains(word))
            {
                continue;
            }
            kept.Add(word);
        }
        return string.Join(' ', kept);
    }

    /// <summary>
    /// Returns the first 5 digits of a postal code, or null if input is empty/has
    /// fewer than 5 digits. Strips whitespace, hyphens, and any +4 suffix.
    /// </summary>
    private static string? TryGetZipFive(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        Span<char> digits = stackalloc char[5];
        var written = 0;
        foreach (var ch in postalCode)
        {
            if (!char.IsDigit(ch)) continue;
            digits[written++] = ch;
            if (written == 5) return new string(digits);
        }

        return null;
    }
}
