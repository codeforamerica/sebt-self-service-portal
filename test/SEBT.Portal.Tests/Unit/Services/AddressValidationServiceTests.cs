using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class AddressValidationServiceTests
{
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

    private static AddressValidationService CreateService(AddressValidationDataSettings settings) =>
        new(Options.Create(settings));

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
}
