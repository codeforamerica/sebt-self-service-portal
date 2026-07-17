using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Api.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Services;

public class DevelopmentPhoneOverrideProviderTests
{
    [Fact]
    public void GetOverridePhone_WhenNotDevelopment_ReturnsNull()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        var options = Options.Create(new DevelopmentPhoneOverrideOptions { Phone = "8185558437" });
        var provider = new DevelopmentPhoneOverrideProvider(environment, options);

        var result = provider.GetOverridePhone();

        Assert.Null(result);
    }

    [Fact]
    public void GetOverridePhone_WhenPhoneIsNull_ReturnsNull()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        var options = Options.Create(new DevelopmentPhoneOverrideOptions { Phone = null! });
        var provider = new DevelopmentPhoneOverrideProvider(environment, options);

        var result = provider.GetOverridePhone();

        Assert.Null(result);
    }

    [Fact]
    public void GetOverridePhone_WhenPhoneIsEmpty_ReturnsNull()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        var options = Options.Create(new DevelopmentPhoneOverrideOptions { Phone = "" });
        var provider = new DevelopmentPhoneOverrideProvider(environment, options);

        var result = provider.GetOverridePhone();

        Assert.Null(result);
    }

    [Fact]
    public void GetOverridePhone_WhenPhoneIsWhitespace_ReturnsNull()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        var options = Options.Create(new DevelopmentPhoneOverrideOptions { Phone = "   " });
        var provider = new DevelopmentPhoneOverrideProvider(environment, options);

        var result = provider.GetOverridePhone();

        Assert.Null(result);
    }

    [Fact]
    public void GetOverridePhone_WhenPhoneHasFewerThan10Digits_ReturnsNull()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        var options = Options.Create(new DevelopmentPhoneOverrideOptions { Phone = "123456789" });
        var provider = new DevelopmentPhoneOverrideProvider(environment, options);

        var result = provider.GetOverridePhone();

        Assert.Null(result);
    }

    [Fact]
    public void GetOverridePhone_WhenDevelopmentAndValidPhone_ReturnsDigitsOnly()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        var options = Options.Create(new DevelopmentPhoneOverrideOptions { Phone = "818-555-8437" });
        var provider = new DevelopmentPhoneOverrideProvider(environment, options);

        var result = provider.GetOverridePhone();

        Assert.Equal("8185558437", result);
    }
}
