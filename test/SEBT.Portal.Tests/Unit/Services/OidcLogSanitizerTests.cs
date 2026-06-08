using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class OidcLogSanitizerTests
{
    [Fact]
    public void Sanitize_strips_newlines_and_truncates()
    {
        var input = new string('x', 600) + "\r\ntail";
        var result = OidcLogSanitizer.Sanitize(input);

        Assert.DoesNotContain("\r", result);
        Assert.DoesNotContain("\n", result);
        Assert.True(result.Length <= OidcLogSanitizer.MaxDescriptionLength + 1);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void Sanitize_returns_empty_for_null()
    {
        Assert.Equal(string.Empty, OidcLogSanitizer.Sanitize(null));
    }
}
