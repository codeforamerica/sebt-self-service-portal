using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Seeding;

namespace SEBT.Portal.Tests.Unit.AppSettings;

public class SeedingSettingsTests
{
    [Fact]
    public void BuildEmail_UsesOverride_ForCoLoadedScenario()
    {
        var settings = new SeedingSettings
        {
            EmailPattern = "{0}@example.com",
            CoLoadedSeedEmailOverride = "co-loaded-override@example.com",
        };

        Assert.Equal("co-loaded-override@example.com", settings.BuildEmail(SeedScenarios.CoLoaded.Name));
    }

    [Fact]
    public void BuildEmail_UsesPattern_WhenOverrideUnset()
    {
        var settings = new SeedingSettings { EmailPattern = "{0}@example.com" };

        Assert.Equal("co-loaded@example.com", settings.BuildEmail(SeedScenarios.CoLoaded.Name));
    }

    [Fact]
    public void BuildEmail_UsesPattern_ForNonCoLoadedScenario_EvenWithOverride()
    {
        var settings = new SeedingSettings
        {
            EmailPattern = "{0}@example.com",
            CoLoadedSeedEmailOverride = "override@example.com",
        };

        Assert.Equal("verified@example.com", settings.BuildEmail(SeedScenarios.Verified.Name));
    }
}
