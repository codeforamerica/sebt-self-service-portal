using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Services;

public class MinimumIalServiceTests
{
    private static MinimumIalService CreateService(MinimumIalSettings? settings = null)
    {
        settings ??= new MinimumIalSettings
        {
            ApplicationCases = IalLevel.IAL1,
            CoLoadedStreamlineCases = IalLevel.IAL1,
            NonCoLoadedStreamlineCases = IalLevel.IAL1plus
        };
        var snapshot = Substitute.For<IOptionsSnapshot<MinimumIalSettings>>();
        snapshot.Value.Returns(settings);
        return new MinimumIalService(snapshot, NullLogger<MinimumIalService>.Instance);
    }

    private static SummerEbtCase CreateCase(
        bool isStreamlineCertified = false,
        bool isCoLoaded = false)
    {
        return new SummerEbtCase
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = isStreamlineCertified,
            IsCoLoaded = isCoLoaded
        };
    }

    [Fact]
    public void GetMinimumIal_WhenNoCases_ReturnsIal1()
    {
        var service = CreateService();
        var result = service.GetMinimumIal([]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void GetMinimumIal_WhenOnlyApplicationCases_ReturnsApplicationCasesLevel()
    {
        var settings = new MinimumIalSettings { ApplicationCases = IalLevel.IAL1 };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase> { CreateCase(isStreamlineCertified: false, isCoLoaded: false) };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void GetMinimumIal_WhenOnlyCoLoadedStreamlineCases_ReturnsCoLoadedLevel()
    {
        var settings = new MinimumIalSettings { CoLoadedStreamlineCases = IalLevel.IAL1 };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase> { CreateCase(isStreamlineCertified: true, isCoLoaded: true) };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void GetMinimumIal_WhenAnyNonCoLoadedStreamlineCase_ReturnsNonCoLoadedLevel()
    {
        var settings = new MinimumIalSettings { NonCoLoadedStreamlineCases = IalLevel.IAL1plus };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase> { CreateCase(isStreamlineCertified: true, isCoLoaded: false) };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void GetMinimumIal_WhenMixedCases_HighestWins()
    {
        var settings = new MinimumIalSettings
        {
            CoLoadedStreamlineCases = IalLevel.IAL1,
            NonCoLoadedStreamlineCases = IalLevel.IAL1plus
        };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase>
        {
            CreateCase(isStreamlineCertified: true, isCoLoaded: true),
            CreateCase(isStreamlineCertified: true, isCoLoaded: false)
        };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void GetMinimumIal_WhenMixedApplicationAndStreamline_HighestWins()
    {
        var settings = new MinimumIalSettings
        {
            ApplicationCases = IalLevel.IAL1,
            NonCoLoadedStreamlineCases = IalLevel.IAL1plus
        };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase>
        {
            CreateCase(isStreamlineCertified: false, isCoLoaded: false),
            CreateCase(isStreamlineCertified: true, isCoLoaded: false)
        };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void GetMinimumIal_WhenAllThreeTypes_HighestWins()
    {
        var settings = new MinimumIalSettings
        {
            ApplicationCases = IalLevel.IAL1,
            CoLoadedStreamlineCases = IalLevel.IAL1,
            NonCoLoadedStreamlineCases = IalLevel.IAL1plus
        };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase>
        {
            CreateCase(isStreamlineCertified: false, isCoLoaded: false),
            CreateCase(isStreamlineCertified: true, isCoLoaded: true),
            CreateCase(isStreamlineCertified: true, isCoLoaded: false)
        };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void GetMinimumIal_WhenApplicationAndCoLoadedOnly_ReturnsIal1()
    {
        var settings = new MinimumIalSettings
        {
            ApplicationCases = IalLevel.IAL1,
            CoLoadedStreamlineCases = IalLevel.IAL1,
            NonCoLoadedStreamlineCases = IalLevel.IAL1plus
        };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase>
        {
            CreateCase(isStreamlineCertified: false, isCoLoaded: false),
            CreateCase(isStreamlineCertified: true, isCoLoaded: true)
        };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void GetMinimumIal_RespectsCustomConfig_WhenCoLoadedRequiresIal1plus()
    {
        var settings = new MinimumIalSettings
        {
            ApplicationCases = IalLevel.IAL1,
            CoLoadedStreamlineCases = IalLevel.IAL1plus,
            NonCoLoadedStreamlineCases = IalLevel.IAL1plus
        };
        var service = CreateService(settings);
        var cases = new List<SummerEbtCase> { CreateCase(isStreamlineCertified: true, isCoLoaded: true) };
        var result = service.GetMinimumIal(cases);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }
}
