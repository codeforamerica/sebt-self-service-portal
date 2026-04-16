using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class ConfigureIdProofingRequirementsTests
{
    private static (ConfigureIdProofingRequirements binder, IdProofingRequirementsSettings settings)
        BindConfig(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var logger = NullLogger<ConfigureIdProofingRequirements>.Instance;
        var binder = new ConfigureIdProofingRequirements(config, logger);
        var settings = new IdProofingRequirementsSettings();
        binder.Configure(settings);
        return (binder, settings);
    }

    [Fact]
    public void Configure_SimpleStringValue_CreatesUniformRequirement()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:address+view"] = "IAL1plus"
        });

        var req = settings.Get(ProtectedResource.Address, ProtectedAction.View);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));
    }

    [Fact]
    public void Configure_ObjectValue_CreatesPerCaseTypeRequirement()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:household+view:application"] = "IAL1",
            ["IdProofingRequirements:household+view:coloadedStreamline"] = "IAL1",
            ["IdProofingRequirements:household+view:streamline"] = "IAL1plus"
        });

        var req = settings.Get(ProtectedResource.Household, ProtectedAction.View);
        var nonCoLoaded = new SummerEbtCase
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = true,
            IsCoLoaded = false
        };
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([nonCoLoaded]));
    }

    [Fact]
    public void Configure_MissingKey_ReturnsDefault_Ial1plus()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:address+view"] = "IAL1plus"
        });

        // card+write not in config — should get default (IAL1plus)
        var req = settings.Get(ProtectedResource.Card, ProtectedAction.Write);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));
    }

    [Fact]
    public void Configure_CaseInsensitiveEnumParsing()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:address+view"] = "ial1plus"
        });

        var req = settings.Get(ProtectedResource.Address, ProtectedAction.View);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));
    }

    [Fact]
    public void Configure_UnknownKey_LogsWarning()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdProofingRequirements:typo+view"] = "IAL1plus"
            })
            .Build();

        var logger = Substitute.For<ILogger<ConfigureIdProofingRequirements>>();
        var binder = new ConfigureIdProofingRequirements(config, logger);
        var settings = new IdProofingRequirementsSettings();

        binder.Configure(settings);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("typo+view")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Configure_InvalidEnumValue_SkipsKeyAndLogsError()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdProofingRequirements:address+view"] = "NotAValidLevel"
            })
            .Build();

        var logger = Substitute.For<ILogger<ConfigureIdProofingRequirements>>();
        var binder = new ConfigureIdProofingRequirements(config, logger);
        var settings = new IdProofingRequirementsSettings();

        binder.Configure(settings);

        // Key was skipped — should fall back to IAL1plus default
        var req = settings.Get(ProtectedResource.Address, ProtectedAction.View);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));

        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("NotAValidLevel")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Configure_NullSubValueInObjectForm_SkipsSubKeyAndLogsError()
    {
        // Simulate a null sub-value: in-memory config with a key that has no value
        // .NET config treats "key": null as a key with null value
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdProofingRequirements:household+view:application"] = null,
                ["IdProofingRequirements:household+view:streamline"] = "IAL1plus"
            })
            .Build();

        var logger = Substitute.For<ILogger<ConfigureIdProofingRequirements>>();
        var binder = new ConfigureIdProofingRequirements(config, logger);
        var settings = new IdProofingRequirementsSettings();

        binder.Configure(settings);

        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("(null)")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Configure_ObjectForm_CaseInsensitiveSubKeys()
    {
        // Config uses uppercase "Application" — should still resolve correctly
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:household+view:Application"] = "IAL1",
            ["IdProofingRequirements:household+view:streamline"] = "IAL1plus"
        });

        var req = settings.Get(ProtectedResource.Household, ProtectedAction.View);
        var appCase = new SummerEbtCase
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = false,
            IsCoLoaded = false
        };
        Assert.Equal(UserIalLevel.IAL1, req.Resolve([appCase]));
    }
}
