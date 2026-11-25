using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.Api.Startup;
using SEBT.Portal.Core.AppSettings;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Startup;

public class SeededDataResetPolicyTests
{
    private static IHostEnvironment EnvironmentNamed(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }

    [Fact]
    public void Decide_WhenDefaultSettingsInStaging_ReturnsNone()
    {
        Assert.Equal(SeededDataReset.None, SeededDataResetPolicy.Decide(
            new SeedingSettings { Enabled = true }, EnvironmentNamed(Environments.Staging)));
    }

    [Fact]
    public void Decide_WhenSettingsNullInStaging_ReturnsNone()
    {
        Assert.Equal(SeededDataReset.None, SeededDataResetPolicy.Decide(null, EnvironmentNamed(Environments.Staging)));
    }

    [Fact]
    public void Decide_WhenClearDataOnStartupInStaging_ReturnsAllUsers()
    {
        Assert.Equal(SeededDataReset.AllUsers, SeededDataResetPolicy.Decide(
            new SeedingSettings { Enabled = true, ClearDataOnStartup = true },
            EnvironmentNamed(Environments.Staging)));
    }

    [Fact]
    public void Decide_WhenClearDataOnStartupWithoutSeedingOutsideDevelopment_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SeededDataResetPolicy.Decide(
                new SeedingSettings { Enabled = false, ClearDataOnStartup = true },
                EnvironmentNamed(Environments.Staging)));

        Assert.Contains("Seeding:Enabled", exception.Message);
    }

    [Fact]
    public void Decide_WhenDevelopmentWithoutFlag_ReturnsSeededUsers()
    {
        Assert.Equal(SeededDataReset.SeededUsers, SeededDataResetPolicy.Decide(
            new SeedingSettings(), EnvironmentNamed(Environments.Development)));
    }

    [Fact]
    public void Decide_WhenDevelopmentWithFlag_AllUsersWins()
    {
        Assert.Equal(SeededDataReset.AllUsers, SeededDataResetPolicy.Decide(
            new SeedingSettings { ClearDataOnStartup = true },
            EnvironmentNamed(Environments.Development)));
    }

    [Fact]
    public void Decide_WhenClearDataOnStartupInProduction_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SeededDataResetPolicy.Decide(
                new SeedingSettings { ClearDataOnStartup = true },
                EnvironmentNamed(Environments.Production)));

        Assert.Contains("ClearDataOnStartup", exception.Message);
    }

    [Fact]
    public void Decide_WhenDefaultSettingsInProduction_ReturnsNone()
    {
        Assert.Equal(SeededDataReset.None, SeededDataResetPolicy.Decide(
            new SeedingSettings(), EnvironmentNamed(Environments.Production)));
    }
}
