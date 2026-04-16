using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models;

public class IalRequirementTests
{
    private static SummerEbtCase ApplicationCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = false,
            IsCoLoaded = false
        };

    private static SummerEbtCase CoLoadedStreamlineCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = true,
            IsCoLoaded = true
        };

    private static SummerEbtCase NonCoLoadedStreamlineCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = true,
            IsCoLoaded = false
        };

    // --- Uniform requirement ---

    [Theory]
    [InlineData(IalLevel.IAL1, UserIalLevel.IAL1)]
    [InlineData(IalLevel.IAL1plus, UserIalLevel.IAL1plus)]
    [InlineData(IalLevel.IAL2, UserIalLevel.IAL2)]
    public void Uniform_Resolve_ReturnsLevel_RegardlessOfCases(
        IalLevel level,
        UserIalLevel expected)
    {
        var req = IalRequirement.Uniform(level);
        var result = req.Resolve([ApplicationCase(), CoLoadedStreamlineCase()]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Uniform_Resolve_ReturnsLevel_WhenNoCases()
    {
        var req = IalRequirement.Uniform(IalLevel.IAL1plus);
        var result = req.Resolve([]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void Uniform_AllLevels_ReturnsSingleLevel()
    {
        var req = IalRequirement.Uniform(IalLevel.IAL1plus);
        Assert.Equal([IalLevel.IAL1plus], req.AllLevels().ToList());
    }

    // --- Per-case-type requirement ---

    [Fact]
    public void PerCaseType_Resolve_ReturnsApplicationCasesLevel()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["ApplicationCases"] = IalLevel.IAL1,
            ["CoLoadedStreamlineCases"] = IalLevel.IAL1,
            ["NonCoLoadedStreamlineCases"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([ApplicationCase()]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void PerCaseType_Resolve_HighestWins_WhenMixedCases()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["ApplicationCases"] = IalLevel.IAL1,
            ["CoLoadedStreamlineCases"] = IalLevel.IAL1,
            ["NonCoLoadedStreamlineCases"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([CoLoadedStreamlineCase(), NonCoLoadedStreamlineCase()]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void PerCaseType_Resolve_ReturnsIal1_WhenNoCases()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["ApplicationCases"] = IalLevel.IAL1plus,
            ["CoLoadedStreamlineCases"] = IalLevel.IAL1plus,
            ["NonCoLoadedStreamlineCases"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void PerCaseType_AllLevels_ReturnsAllConfiguredLevels()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["ApplicationCases"] = IalLevel.IAL1,
            ["NonCoLoadedStreamlineCases"] = IalLevel.IAL1plus
        });

        var levels = req.AllLevels().OrderBy(l => l).ToList();
        Assert.Equal([IalLevel.IAL1, IalLevel.IAL1plus], levels);
    }

    // --- Default requirement ---

    [Fact]
    public void Default_Resolve_ReturnsIal1plus()
    {
        var req = IalRequirement.Default();
        var result = req.Resolve([ApplicationCase()]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }
}
