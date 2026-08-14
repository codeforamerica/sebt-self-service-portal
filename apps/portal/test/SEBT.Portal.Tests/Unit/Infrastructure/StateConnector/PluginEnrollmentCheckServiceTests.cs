using NSubstitute;
using SEBT.Portal.Core.StateConnector;
using SEBT.Portal.Infrastructure.StateConnector;
using IPluginEnrollmentCheckService = SEBT.Portal.StatesPlugins.Interfaces.IEnrollmentCheckService;
using PluginChildCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckResult;
using PluginEligibilityType = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EligibilityType;
using PluginEnrollmentCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckRequest;
using PluginEnrollmentCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckResult;
using PluginEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateConnector;

/// <summary>
/// Verifies that <see cref="PluginEnrollmentCheckService"/> maps every field between the
/// Core port models and the plugin contract models, in both directions.
/// </summary>
public class PluginEnrollmentCheckServiceTests
{
    private readonly IPluginEnrollmentCheckService _plugin =
        Substitute.For<IPluginEnrollmentCheckService>();

    private readonly PluginEnrollmentCheckService _sut;

    public PluginEnrollmentCheckServiceTests()
    {
        _sut = new PluginEnrollmentCheckService(_plugin);
    }

    private static PluginEnrollmentCheckResult EmptyPluginResult() =>
        new() { Results = [] };

    public static TheoryData<PluginEnrollmentStatus> AllPluginEnrollmentStatuses()
    {
        var data = new TheoryData<PluginEnrollmentStatus>();
        foreach (var value in Enum.GetValues<PluginEnrollmentStatus>())
        {
            data.Add(value);
        }
        return data;
    }

    public static TheoryData<PluginEligibilityType> AllPluginEligibilityTypes()
    {
        var data = new TheoryData<PluginEligibilityType>();
        foreach (var value in Enum.GetValues<PluginEligibilityType>())
        {
            data.Add(value);
        }
        return data;
    }

    [Fact]
    public async Task CheckEnrollmentAsync_MapsFullyPopulatedRequestToPlugin()
    {
        var checkId = Guid.NewGuid();
        var request = new EnrollmentCheckRequest
        {
            Children =
            [
                new ChildCheckRequest
                {
                    CheckId = checkId,
                    FirstName = "Jamie",
                    LastName = "Rivera",
                    DateOfBirth = new DateOnly(2015, 3, 14),
                    SchoolName = "Oak Hill Elementary",
                    SchoolCode = "OHE-42",
                    AdditionalFields = new Dictionary<string, string>
                    {
                        ["grade"] = "4",
                        ["district"] = "11"
                    }
                }
            ],
            GuardianContactInfo = "guardian@example.com"
        };

        PluginEnrollmentCheckRequest? captured = null;
        using var cts = new CancellationTokenSource();
        _plugin.CheckEnrollmentAsync(
                Arg.Do<PluginEnrollmentCheckRequest>(r => captured = r),
                cts.Token)
            .Returns(EmptyPluginResult());

        await _sut.CheckEnrollmentAsync(request, cts.Token);

        Assert.NotNull(captured);
        Assert.Equal("guardian@example.com", captured.GuardianContactInfo);
        var child = Assert.Single(captured.Children);
        Assert.Equal(checkId, child.CheckId);
        Assert.Equal("Jamie", child.FirstName);
        Assert.Equal("Rivera", child.LastName);
        Assert.Equal(new DateOnly(2015, 3, 14), child.DateOfBirth);
        Assert.Equal("Oak Hill Elementary", child.SchoolName);
        Assert.Equal("OHE-42", child.SchoolCode);
        Assert.Equal(
            new Dictionary<string, string> { ["grade"] = "4", ["district"] = "11" },
            child.AdditionalFields);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_PreservesNullOptionalRequestFields()
    {
        var request = new EnrollmentCheckRequest
        {
            Children =
            [
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Sam",
                    LastName = "Lee",
                    DateOfBirth = new DateOnly(2016, 7, 2)
                }
            ]
        };

        PluginEnrollmentCheckRequest? captured = null;
        _plugin.CheckEnrollmentAsync(
                Arg.Do<PluginEnrollmentCheckRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(EmptyPluginResult());

        await _sut.CheckEnrollmentAsync(request);

        Assert.NotNull(captured);
        Assert.Null(captured.GuardianContactInfo);
        var child = Assert.Single(captured.Children);
        Assert.Null(child.SchoolName);
        Assert.Null(child.SchoolCode);
        Assert.Empty(child.AdditionalFields);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_MapsFullyPopulatedPluginResultToCore()
    {
        var checkId = Guid.NewGuid();
        var details = new Dictionary<string, object>
        {
            ["matchSource"] = "CBMS",
            ["score"] = 0.97
        };
        _plugin.CheckEnrollmentAsync(
                Arg.Any<PluginEnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginEnrollmentCheckResult
            {
                Results =
                [
                    new PluginChildCheckResult
                    {
                        CheckId = checkId,
                        FirstName = "Jamie",
                        LastName = "Rivera",
                        DateOfBirth = new DateOnly(2015, 3, 14),
                        Status = PluginEnrollmentStatus.PossibleMatch,
                        MatchConfidence = 0.97,
                        StatusMessage = "Fuzzy match on name",
                        EligibilityType = PluginEligibilityType.Snap,
                        SchoolName = "Oak Hill Elementary",
                        Details = details
                    }
                ],
                ResponseMessage = "1 of 1 matched"
            });

        var result = await _sut.CheckEnrollmentAsync(MinimalRequest());

        Assert.Equal("1 of 1 matched", result.ResponseMessage);
        var child = Assert.Single(result.Results);
        Assert.Equal(checkId, child.CheckId);
        Assert.Equal("Jamie", child.FirstName);
        Assert.Equal("Rivera", child.LastName);
        Assert.Equal(new DateOnly(2015, 3, 14), child.DateOfBirth);
        Assert.Equal(EnrollmentStatus.PossibleMatch, child.Status);
        Assert.Equal(0.97, child.MatchConfidence);
        Assert.Equal("Fuzzy match on name", child.StatusMessage);
        Assert.Equal(EligibilityType.Snap, child.EligibilityType);
        Assert.Equal("Oak Hill Elementary", child.SchoolName);
        Assert.Equal(details, child.Details);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_PreservesNullOptionalResultFields()
    {
        _plugin.CheckEnrollmentAsync(
                Arg.Any<PluginEnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginEnrollmentCheckResult
            {
                Results =
                [
                    new PluginChildCheckResult
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Sam",
                        LastName = "Lee",
                        DateOfBirth = new DateOnly(2016, 7, 2),
                        Status = PluginEnrollmentStatus.NonMatch
                    }
                ]
            });

        var result = await _sut.CheckEnrollmentAsync(MinimalRequest());

        Assert.Null(result.ResponseMessage);
        var child = Assert.Single(result.Results);
        Assert.Null(child.MatchConfidence);
        Assert.Null(child.StatusMessage);
        Assert.Null(child.EligibilityType);
        Assert.Null(child.SchoolName);
        Assert.Empty(child.Details);
    }

    [Theory]
    [MemberData(nameof(AllPluginEnrollmentStatuses))]
    public async Task CheckEnrollmentAsync_MapsEveryEnrollmentStatusByName(
        PluginEnrollmentStatus pluginStatus)
    {
        _plugin.CheckEnrollmentAsync(
                Arg.Any<PluginEnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginEnrollmentCheckResult
            {
                Results =
                [
                    new PluginChildCheckResult
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Sam",
                        LastName = "Lee",
                        DateOfBirth = new DateOnly(2016, 7, 2),
                        Status = pluginStatus
                    }
                ]
            });

        var result = await _sut.CheckEnrollmentAsync(MinimalRequest());

        var child = Assert.Single(result.Results);
        Assert.Equal(pluginStatus.ToString(), child.Status.ToString());
    }

    [Theory]
    [MemberData(nameof(AllPluginEligibilityTypes))]
    public async Task CheckEnrollmentAsync_MapsEveryEligibilityTypeByName(
        PluginEligibilityType pluginEligibilityType)
    {
        _plugin.CheckEnrollmentAsync(
                Arg.Any<PluginEnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginEnrollmentCheckResult
            {
                Results =
                [
                    new PluginChildCheckResult
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Sam",
                        LastName = "Lee",
                        DateOfBirth = new DateOnly(2016, 7, 2),
                        Status = PluginEnrollmentStatus.Match,
                        EligibilityType = pluginEligibilityType
                    }
                ]
            });

        var result = await _sut.CheckEnrollmentAsync(MinimalRequest());

        var child = Assert.Single(result.Results);
        Assert.NotNull(child.EligibilityType);
        Assert.Equal(pluginEligibilityType.ToString(), child.EligibilityType.ToString());
    }

    private static EnrollmentCheckRequest MinimalRequest() =>
        new()
        {
            Children =
            [
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Sam",
                    LastName = "Lee",
                    DateOfBirth = new DateOnly(2016, 7, 2)
                }
            ]
        };
}
