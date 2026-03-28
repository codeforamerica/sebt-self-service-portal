using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class AddressValidationServiceTests
{
    // --- Blocked address detection ---

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenAddressIsBlockedForDc()
    {
        var service = new AddressValidationService("dc");
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
        var service = new AddressValidationService("co");
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
        var service = new AddressValidationService("dc");
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
        var service = new AddressValidationService("dc");
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
        var service = new AddressValidationService("co");
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
        var service = new AddressValidationService("dc");
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
        var service = new AddressValidationService("co");
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
        var service = new AddressValidationService("dc");
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

    // --- DC street abbreviation ---

    [Fact]
    public async Task ValidateAsync_ReturnsSuggestion_WhenDcStreetCanBeAbbreviated()
    {
        var service = new AddressValidationService("dc");
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
    public async Task ValidateAsync_ReturnsSuggestion_WhenDcStreetNannieHelenBurroughsCanBeAbbreviated()
    {
        var service = new AddressValidationService("dc");
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
        var service = new AddressValidationService("dc");
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
    public async Task ValidateAsync_DoesNotAbbreviate_WhenStreetIsUnder30Chars()
    {
        var service = new AddressValidationService("dc");
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
    public async Task ValidateAsync_DoesNotAbbreviate_ForCoAddresses()
    {
        var service = new AddressValidationService("co");
        var address = new Address
        {
            StreetAddress1 = "1234 Martin Luther King Jr Blvd",
            City = "Denver",
            State = "Colorado",
            PostalCode = "80205"
        };

        var result = await service.ValidateAsync(address);

        // CO has no abbreviation rules, so even a long street with a known name stays valid
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenDcStreetExceeds30CharsAndCannotBeAbbreviated()
    {
        var service = new AddressValidationService("dc");
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
}
