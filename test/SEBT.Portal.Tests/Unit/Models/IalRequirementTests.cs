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
    public void PerCaseType_Resolve_ReturnsapplicationLevel()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1,
            ["coloadedStreamline"] = IalLevel.IAL1,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([ApplicationCase()]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void PerCaseType_Resolve_HighestWins_WhenMixedCases()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1,
            ["coloadedStreamline"] = IalLevel.IAL1,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([CoLoadedStreamlineCase(), NonCoLoadedStreamlineCase()]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void PerCaseType_Resolve_ReturnsIal1_WhenNoCases()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1plus,
            ["coloadedStreamline"] = IalLevel.IAL1plus,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void PerCaseType_AllLevels_ReturnsAllConfiguredLevels()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1,
            ["streamline"] = IalLevel.IAL1plus
        });

        var levels = req.AllLevels().OrderBy(l => l).ToList();
        Assert.Equal([IalLevel.IAL1, IalLevel.IAL1plus], levels);
    }

    // --- Per-case-type: empty cases returns IAL1 (no cases = no elevated requirement) ---

    [Fact]
    public void PerCaseType_Resolve_EmptyCases_ReturnsIal1_NotHighestConfiguredLevel()
    {
        // This is intentional: when there are no cases to evaluate, there is no
        // case-derived reason to require elevated IAL. The user still needs to meet
        // any uniform requirements on the same resource (e.g., address+view: IAL1plus).
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1plus,
            ["coloadedStreamline"] = IalLevel.IAL1plus,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    // --- Per-case-type: missing case-type key falls back to IAL1plus (fail-closed) ---

    [Fact]
    public void PerCaseType_Resolve_MissingCaseTypeKey_FallsBackToIal1plus()
    {
        // Config only has coloadedStreamline. An ApplicationCase lookup
        // should fall back to IAL1plus (fail-closed), not IAL1.
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["coloadedStreamline"] = IalLevel.IAL1
        });

        var result = req.Resolve([ApplicationCase()]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
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
