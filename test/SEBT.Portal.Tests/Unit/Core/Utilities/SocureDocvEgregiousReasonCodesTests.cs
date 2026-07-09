using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Tests.Unit.Core.Utilities;

public class SocureDocvEgregiousReasonCodesTests
{
    private static SocureDocvEgregiousReasonRejectionSettings EnabledSettings() => new()
    {
        Enabled = true,
        ReasonCodes = ["R815", "R836"]
    };

    [Fact]
    public void GetMatchingEgregiousCodes_ReturnsMatches_WhenConfiguredCodePresent()
    {
        var matches = SocureDocvEgregiousReasonCodes.GetMatchingEgregiousCodes(
            EnabledSettings(),
            ["I520", "R815"]);

        Assert.NotNull(matches);
        Assert.Single(matches);
        Assert.Equal("R815", matches[0]);
    }

    [Fact]
    public void GetMatchingEgregiousCodes_IsCaseInsensitive()
    {
        var matches = SocureDocvEgregiousReasonCodes.GetMatchingEgregiousCodes(
            EnabledSettings(),
            ["r836"]);

        Assert.NotNull(matches);
        Assert.Single(matches);
        Assert.Equal("r836", matches[0]);
    }

    [Fact]
    public void GetMatchingEgregiousCodes_ReturnsNull_WhenDisabled()
    {
        var settings = EnabledSettings();
        settings.Enabled = false;

        var matches = SocureDocvEgregiousReasonCodes.GetMatchingEgregiousCodes(
            settings,
            ["R815"]);

        Assert.Null(matches);
    }
}
