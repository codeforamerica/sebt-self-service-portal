using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Tests.Unit.Core.Utilities;

public class SocureDocvEgregiousReasonCooldownTests
{
    private static SocureDocvEgregiousReasonCooldownSettings EnabledSettings() => new()
    {
        Enabled = true,
        CooldownDays = 14,
        ReasonCodes = ["R815", "R836"]
    };

    [Fact]
    public void IsUserInCooldown_ReturnsTrue_WhenCooldownUntilIsInFuture()
    {
        var user = new User { IdProofingCooldownUntil = DateTime.UtcNow.AddDays(1) };

        Assert.True(SocureDocvEgregiousReasonCooldown.IsUserInCooldown(user, DateTime.UtcNow));
    }

    [Fact]
    public void IsUserInCooldown_ReturnsFalse_WhenCooldownUntilIsInPast()
    {
        var user = new User { IdProofingCooldownUntil = DateTime.UtcNow.AddMinutes(-1) };

        Assert.False(SocureDocvEgregiousReasonCooldown.IsUserInCooldown(user, DateTime.UtcNow));
    }

    [Fact]
    public void GetMatchingEgregiousCodes_ReturnsMatches_WhenConfiguredCodePresent()
    {
        var matches = SocureDocvEgregiousReasonCooldown.GetMatchingEgregiousCodes(
            EnabledSettings(),
            ["I520", "R815"]);

        Assert.NotNull(matches);
        Assert.Single(matches);
        Assert.Equal("R815", matches[0]);
    }

    [Fact]
    public void GetMatchingEgregiousCodes_IsCaseInsensitive()
    {
        var matches = SocureDocvEgregiousReasonCooldown.GetMatchingEgregiousCodes(
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

        var matches = SocureDocvEgregiousReasonCooldown.GetMatchingEgregiousCodes(
            settings,
            ["R815"]);

        Assert.Null(matches);
    }

    [Fact]
    public void ComputeCooldownUntil_PreservesLongerExistingCooldown()
    {
        var utcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { IdProofingCooldownUntil = utcNow.AddDays(30) };

        var result = SocureDocvEgregiousReasonCooldown.ComputeCooldownUntil(
            EnabledSettings(),
            user,
            utcNow);

        Assert.Equal(utcNow.AddDays(30), result);
    }

    [Fact]
    public void ComputeCooldownUntil_UsesConfiguredDays_WhenNoExistingCooldown()
    {
        var utcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User();

        var result = SocureDocvEgregiousReasonCooldown.ComputeCooldownUntil(
            EnabledSettings(),
            user,
            utcNow);

        Assert.Equal(utcNow.AddDays(14), result);
    }
}
