using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class AddressValidationServiceTests
{
    private sealed class StubBlockedAddressDataSource(params BlockedAddressEntry[] entries) : IBlockedAddressDataSource
    {
        public IReadOnlyCollection<BlockedAddressEntry> GetEntries() => entries;
    }

    /// <summary>
    /// DC settings: 5 blocked addresses, 6 street abbreviation mappings, 30-char limit.
    /// Mirrors the data that would be in appsettings.dc.json.
    /// </summary>
    private static AddressValidationDataSettings DcSettings => new()
    {
        BlockedAddresses =
        [
            "2100 Martin Luther King Jr Avenue SE",
            "3851 Alabama Avenue SE",
            "4049 South Capitol Street SW",
            "645 H Street NE",
            "1207 Taylor Street NW"
        ],
        StreetAbbreviations = new Dictionary<string, string>
        {
            ["ALBERT IRVIN CASSELL"] = "ALBERT IRVIN CASS",
            ["COMMODORE JOSHUA BARNEY"] = "COMMODORE JOSH BARN",
            ["MARTIN LUTHER KING JR"] = "MLK JR",
            ["NANNIE HELEN BURROUGHS"] = "N H BURROUGHS",
            ["PATRICIA ROBERTS HARRIS"] = "PATRICIA RBRTS HARR",
            ["ROBERT CLIFTON WEAVER"] = "ROBRT CLIFTN WEAVR"
        },
        MaxStreetAddressLength = 30
    };

    /// <summary>
    /// CO settings: 1 blocked address, no abbreviations, no length limit.
    /// Mirrors the data that would be in appsettings.co.json.
    /// </summary>
    private static AddressValidationDataSettings CoSettings => new()
    {
        BlockedAddresses =
        [
            "1575 Sherman St"
        ]
    };

    private static AddressValidationService CreateService(
        AddressValidationDataSettings settings,
        IBlockedAddressDataSource? blockedAddressData = null) =>
        new(Options.Create(settings), blockedAddressData ?? new EmptyBlockedAddressDataSource());

    // --- Blocked address detection ---

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenAddressIsBlockedForDc()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "2100 Martin Luther King Jr Avenue SE",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20020"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenAddressIsBlockedForCo()
    {
        var service = CreateService(CoSettings);
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman St",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_BlockedAddressCheck_IsCaseInsensitive()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "645 h street ne",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20002"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValid_WhenAddressIsNotBlocked()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_DcBlockedAddresses_DoNotApplyToCo()
    {
        // CO settings don't include DC blocked addresses, so a DC address should pass
        var service = CreateService(CoSettings);
        var address = new Address
        {
            StreetAddress1 = "2100 Martin Luther King Jr Avenue SE",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20020"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }

    // --- Street type normalization ---

    [Theory]
    [InlineData("645 H St NE")]
    [InlineData("645 H st NE")]
    [InlineData("645 H ST NE")]
    public async Task ValidateAsync_BlocksAbbreviatedStreetType_WhenFullFormIsInBlockedList(string streetAddress)
    {
        // "645 H Street NE" is in the DC blocked list; abbreviated forms should also match
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = streetAddress,
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20002"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.Equal("blocked", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_CoAbbreviatedBlockedEntry_StillMatchesAfterNormalization()
    {
        // CO blocked list stores "1575 Sherman St" (abbreviated form).
        // After normalization expands to "1575 Sherman Street", it should still match.
        var service = CreateService(CoSettings);
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman Street",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.Equal("blocked", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotMangleStreetNamesContainingAbbreviationSubstrings()
    {
        // "Stanton" contains "St" but should NOT be expanded to "Streetanton"
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "100 Stanton Pl NE",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20002"
        };

        var result = await service.ValidateAsync(address);

        // This address is not blocked, so it should be valid
        Assert.True(result.IsValid);
    }

    // --- Street abbreviation (length limit) ---

    [Fact]
    public async Task ValidateAsync_ReturnsSuggestion_WhenStreetCanBeAbbreviated()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "1234 Martin Luther King Jr Ave NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.NotNull(result.SuggestedAddress);
        Assert.Contains("MLK JR", result.SuggestedAddress!.StreetAddress1!.ToUpperInvariant());
    }

    [Fact]
    public async Task ValidateAsync_ReturnsSuggestion_WhenNannieHelenBurroughsCanBeAbbreviated()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "1400 Nannie Helen Burroughs Ave NE",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20019"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.NotNull(result.SuggestedAddress);
        Assert.Contains("N H BURROUGHS", result.SuggestedAddress!.StreetAddress1!.ToUpperInvariant());
    }

    [Fact]
    public async Task ValidateAsync_PreservesOtherAddressFields_WhenAbbreviating()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "1234 Martin Luther King Jr Ave NW",
            StreetAddress2 = "Apt 4B",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await service.ValidateAsync(address);

        Assert.NotNull(result.SuggestedAddress);
        Assert.Equal("Apt 4B", result.SuggestedAddress!.StreetAddress2);
        Assert.Equal("Washington", result.SuggestedAddress.City);
        Assert.Equal("District of Columbia", result.SuggestedAddress.State);
        Assert.Equal("20001", result.SuggestedAddress.PostalCode);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotAbbreviate_WhenStreetIsUnderMaxLength()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            // "123 MLK Jr Ave NW" is under 30 chars, even though it contains a known street
            StreetAddress1 = "123 MLK Jr Ave NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
        Assert.Null(result.SuggestedAddress);
    }

    [Fact]
    public async Task ValidateAsync_DoesNotAbbreviate_WhenNoLengthLimitConfigured()
    {
        // CO has no MaxStreetAddressLength (defaults to 0), so even a long street stays valid
        var service = CreateService(CoSettings);
        var address = new Address
        {
            StreetAddress1 = "1234 Martin Luther King Jr Blvd",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80205"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenStreetExceedsMaxLengthAndCannotBeAbbreviated()
    {
        var service = CreateService(DcSettings);
        var address = new Address
        {
            StreetAddress1 = "12345 Some Very Long Unknown Street Name NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(result.SuggestedAddress);
    }

    // --- Empty settings (no blocked addresses, no abbreviations, no limit) ---

    [Fact]
    public async Task ValidateAsync_AcceptsAnyAddress_WhenSettingsAreEmpty()
    {
        var service = CreateService(new AddressValidationDataSettings());
        var address = new Address
        {
            StreetAddress1 = "2100 Martin Luther King Jr Avenue SE",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20020"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }

    // --- Data-source-backed blocked addresses (street + 5-digit ZIP) ---

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_BlocksWhenStreetAndZipMatch()
    {
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 Sherman St", "80203")));
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman St",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.Equal("blocked", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_DoesNotBlockSameStreetAtDifferentZip()
    {
        // 333-row CO list spans the state; "100 Main St" at 80205 must not block 80123.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("100 Main St", "80205")));
        var address = new Address
        {
            StreetAddress1 = "100 Main St",
            City = "Littleton",
            State = "Colorado",
            PostalCode = "80123"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_BlocksWhenUserSubmitsZipPlusFour()
    {
        // CSV records ZIP+4 ("80203-1702") but user input may be 5- or 9-digit.
        // The matcher normalizes to first 5.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 Sherman St", "80203")));
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman St",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203-1702"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_RespectsStreetTypeNormalization()
    {
        // CSV stores "ST"; user enters "Street". Both must match through normalization.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 SHERMAN ST", "80203")));
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman Street",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_BlocksPoBoxAtMatchingZip()
    {
        // PO-box-only CSV row synthesizes "PO BOX {n}" as the street key.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("PO BOX 220", "80701")));
        var address = new Address
        {
            StreetAddress1 = "PO Box 220",
            City = "Fort Morgan",
            State = "Colorado",
            PostalCode = "80701"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.Equal("blocked", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_DoesNotBlockOnMissingZip()
    {
        // If user input has no postal code, the ZIP-keyed matcher must not throw and must not match.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 Sherman St", "80203")));
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman St",
            City = "Denver",
            State = "Colorado",
            PostalCode = null
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }

    // --- Smarty directional drift (ZIP-keyed entries only) ---

    [Theory]
    [InlineData("1575 N Sherman St")]   // Smarty adds North directional (the production case)
    [InlineData("1575 S Sherman St")]   // Hypothetical: Smarty adds South
    [InlineData("1575 NE Sherman St")]  // Compound directional inserted before street
    [InlineData("1575 Sherman St NE")]  // Directional appended as suffix (DC-style)
    [InlineData("1575 North Sherman St")] // Full word directional
    public async Task ValidateAsync_DataSourceEntry_BlocksWhenSmartyAddsDirectional(string streetWithDirectional)
    {
        // CSV stores "1575 Sherman St" with no directional; Smarty's canonical form
        // adds one. The ZIP-keyed matcher must reconcile both forms so the blocked
        // address still gets caught after Smarty normalization.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 Sherman St", "80203")));
        var address = new Address
        {
            StreetAddress1 = streetWithDirectional,
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.Equal("blocked", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_DataSourceEntry_BlocksWhenCsvHasDirectionalButUserOmits()
    {
        // Mirror: CSV stores a directional, user input doesn't. Same loose-match
        // behavior applies in the other direction.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 N Sherman St", "80203")));
        var address = new Address
        {
            StreetAddress1 = "1575 Sherman St",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("1575 N Sherman St Ste 200")]    // Suite, abbreviated
    [InlineData("1575 Sherman St Suite 200")]    // Suite, full word
    [InlineData("1575 Sherman St Apt 4B")]       // Apartment
    [InlineData("1575 Sherman St Apartment 4B")] // Apartment, full word
    [InlineData("1575 Sherman St Unit 5")]       // Unit
    [InlineData("1575 Sherman St Fl 7")]         // Floor, abbreviated
    [InlineData("1575 Sherman St Floor 7")]      // Floor, full word
    [InlineData("1575 Sherman St Rm 100")]       // Room
    [InlineData("1575 Sherman St Bldg A Ste 200")] // Multiple unit indicators
    public async Task ValidateAsync_DataSourceEntry_BlocksWhenSmartyKeepsUnitInLine1(string streetWithUnit)
    {
        // Smarty's US Street API sometimes leaves unit indicators (Ste/Apt/Fl/etc.) in
        // streetAddress1 rather than parsing them out into streetAddress2. The CSV
        // entries don't carry unit info in L1, so the loose matcher must drop unit
        // suffixes to keep the comparison equivalent.
        var service = CreateService(
            new AddressValidationDataSettings(),
            new StubBlockedAddressDataSource(new BlockedAddressEntry("1575 Sherman St", "80203")));
        var address = new Address
        {
            StreetAddress1 = streetWithUnit,
            City = "Denver",
            State = "Colorado",
            PostalCode = "80203"
        };

        var result = await service.ValidateAsync(address);

        Assert.False(result.IsValid);
        Assert.Equal("blocked", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_LegacyEntry_DistinguishesDcQuadrants()
    {
        // DC street naming requires the quadrant suffix (NE, NW, SE, SW) to disambiguate.
        // Loose matching applied to legacy L1-only entries would conflate "Taylor St NW"
        // (blocked) with "Taylor St SW" (residential). The legacy path must stay strict.
        var dcSettings = new AddressValidationDataSettings
        {
            BlockedAddresses = ["1207 Taylor Street NW"]
        };
        var service = CreateService(dcSettings);
        var address = new Address
        {
            StreetAddress1 = "1207 Taylor Street SW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20020"
        };

        var result = await service.ValidateAsync(address);

        Assert.True(result.IsValid);
    }
}
