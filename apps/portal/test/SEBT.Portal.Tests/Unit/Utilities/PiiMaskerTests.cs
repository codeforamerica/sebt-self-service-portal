using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Tests.Unit.Utilities;

/// <summary>
/// Unit tests for PiiMasker — verifies partial masking of email, phone, and address fields.
/// </summary>
public class PiiMaskerTests
{
    // ── Email masking ──

    [Theory]
    [InlineData("jane@example.com", "j***@example.com")]
    [InlineData("ab@example.com", "a***@example.com")]
    [InlineData("a@example.com", "a***@example.com")]
    [InlineData("longname@gmail.com", "l***@gmail.com")]
    public void MaskEmail_MasksLocalPart_PreservesDomain(string input, string expected)
    {
        Assert.Equal(expected, PiiMasker.MaskEmail(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskEmail_NullOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(PiiMasker.MaskEmail(input));
    }

    [Fact]
    public void MaskEmail_NoAtSign_ReturnsMaskedFully()
    {
        // Malformed email — mask it anyway rather than exposing
        var result = PiiMasker.MaskEmail("notanemail");
        Assert.NotNull(result);
        Assert.DoesNotContain("notanemail", result);
    }

    // ── Phone masking ──

    [Theory]
    [InlineData("(303) 555-0100", "***-***-0100")]
    [InlineData("303-555-0100", "***-***-0100")]
    [InlineData("3035550100", "***-***-0100")]
    [InlineData("555-0100", "***-0100")]
    public void MaskPhone_ShowsLast4_MasksRest(string input, string expected)
    {
        Assert.Equal(expected, PiiMasker.MaskPhone(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskPhone_NullOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(PiiMasker.MaskPhone(input));
    }

    [Fact]
    public void MaskPhone_TooFewDigits_ReturnsMasked()
    {
        // Less than 4 digits — still mask, don't expose partial
        var result = PiiMasker.MaskPhone("123");
        Assert.NotNull(result);
        Assert.Equal("***", result);
    }

    // ── Address masking ──

    [Fact]
    public void MaskStreetAddress_MasksStreetLines()
    {
        var result = PiiMasker.MaskStreetAddress("123 Main St", "Apt 4B");

        Assert.NotNull(result);
        Assert.DoesNotContain("123 Main St", result);
        Assert.DoesNotContain("Apt 4B", result);
    }

    [Fact]
    public void MaskStreetAddress_NullStreet_ReturnsNull()
    {
        Assert.Null(PiiMasker.MaskStreetAddress(null, null));
    }
}
