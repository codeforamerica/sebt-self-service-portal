using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Tests.Unit.Utilities;

public class SsnNormalizerTests
{
    [Fact]
    public void NormalizeOrNull_WhenFormattedWithDashes_ReturnsDigitsOnly()
    {
        var result = SsnNormalizer.NormalizeOrNull("123-45-6789");

        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NormalizeOrNull_WhenFormattedWithSpaces_ReturnsDigitsOnly()
    {
        var result = SsnNormalizer.NormalizeOrNull("123 45 6789");

        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NormalizeOrNull_WhenHasLeadingTrailingWhitespace_Trims()
    {
        var result = SsnNormalizer.NormalizeOrNull("  123456789  ");

        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NormalizeOrNull_WhenNull_ReturnsNull()
    {
        var result = SsnNormalizer.NormalizeOrNull(null);

        Assert.Null(result);
    }

    [Fact]
    public void NormalizeOrNull_WhenWhitespace_ReturnsNull()
    {
        var result = SsnNormalizer.NormalizeOrNull("   ");

        Assert.Null(result);
    }

    [Fact]
    public void Normalize_WhenFormattedWithDashes_ReturnsDigitsOnly()
    {
        var result = SsnNormalizer.Normalize("123-45-6789");

        Assert.Equal("123456789", result);
    }

    [Fact]
    public void Normalize_WhenNull_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => SsnNormalizer.Normalize(null!));

        Assert.Equal("ssn", ex.ParamName);
    }

    [Fact]
    public void Normalize_WhenWhitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => SsnNormalizer.Normalize("   "));
    }
}
