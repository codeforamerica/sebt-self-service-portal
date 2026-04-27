using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizePhone_returns_null_for_null_empty_or_whitespace(string? input)
    {
        Assert.Null(PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("555-1234")]
    [InlineData("abcdefghij")]
    public void NormalizePhone_returns_null_for_too_short_or_non_numeric(string input)
    {
        Assert.Null(PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("8185551234", "8185551234")]           // bare 10 digits
    [InlineData("(818) 555-1234", "8185551234")]       // US formatted
    [InlineData("818-555-1234", "8185551234")]         // dashes
    [InlineData("818.555.1234", "8185551234")]         // dots
    [InlineData("  818 555 1234  ", "8185551234")]     // spaces with padding
    public void NormalizePhone_extracts_10_digit_national_number(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("18185551234", "8185551234")]          // leading 1 country code
    [InlineData("+18185551234", "8185551234")]         // E.164
    [InlineData("+1 (818) 555-1234", "8185551234")]   // E.164 with formatting
    [InlineData("1-818-555-1234", "8185551234")]       // country code with dash
    public void NormalizePhone_strips_US_country_code(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("+447700900000")]   // UK mobile
    [InlineData("+33612345678")]    // France mobile
    [InlineData("0044770090000")]   // UK with international dialing prefix
    public void NormalizePhone_rejects_non_US_numbers(string input)
    {
        Assert.Null(PhoneNormalizer.Normalize(input));
    }

    [Fact]
    public void NormalizePhone_does_not_double_strip_leading_ones()
    {
        // Area codes can't start with 1 per NANP rules, but libphonenumber
        // should handle the country code correctly without TrimStart bugs.
        // "11234567890" = country code 1 + 1234567890, but 123 is not a valid
        // NANP area code, so this should be rejected as invalid.
        Assert.Null(PhoneNormalizer.Normalize("11234567890"));
    }
}
