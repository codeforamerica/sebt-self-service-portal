using NSubstitute;
using SEBT.Portal.Core.StateConnector;
using SEBT.Portal.Infrastructure.StateConnector;
using IPluginCardReplacementService = SEBT.Portal.StatesPlugins.Interfaces.ICardReplacementService;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateConnector;

/// <summary>
/// Verifies that <see cref="PluginCardReplacementService"/> maps every field between the
/// Core port models and the plugin contract models, in both directions.
/// </summary>
public class PluginCardReplacementServiceTests
{
    private readonly IPluginCardReplacementService _plugin =
        Substitute.For<IPluginCardReplacementService>();

    private readonly PluginCardReplacementService _sut;

    public PluginCardReplacementServiceTests()
    {
        _sut = new PluginCardReplacementService(_plugin);
    }

    public static TheoryData<CardReplacementReason> AllCoreCardReplacementReasons()
    {
        var data = new TheoryData<CardReplacementReason>();
        foreach (var value in Enum.GetValues<CardReplacementReason>())
        {
            data.Add(value);
        }
        return data;
    }

    [Fact]
    public async Task RequestCardReplacementAsync_MapsFullyPopulatedRequestToPlugin()
    {
        var request = new CardReplacementRequest
        {
            HouseholdIdentifierValue = "guardian@example.com",
            CaseRefs =
            [
                new CaseRef
                {
                    SummerEbtCaseId = "CASE-001",
                    ApplicationId = "APP-77",
                    ApplicationStudentId = "STU-9"
                },
                new CaseRef
                {
                    SummerEbtCaseId = "CASE-002"
                }
            ],
            Reason = CardReplacementReason.Lost
        };

        PluginCardReplacementRequest? captured = null;
        using var cts = new CancellationTokenSource();
        _plugin.RequestCardReplacementAsync(
                Arg.Do<PluginCardReplacementRequest>(r => captured = r),
                cts.Token)
            .Returns(PluginCardReplacementResult.Success());

        await _sut.RequestCardReplacementAsync(request, cts.Token);

        Assert.NotNull(captured);
        Assert.Equal("guardian@example.com", captured.HouseholdIdentifierValue);
        Assert.Equal(PluginCardReplacementReason.Lost, captured.Reason);
        Assert.Equal(2, captured.CaseRefs.Count);
        Assert.Equal("CASE-001", captured.CaseRefs[0].SummerEbtCaseId);
        Assert.Equal("APP-77", captured.CaseRefs[0].ApplicationId);
        Assert.Equal("STU-9", captured.CaseRefs[0].ApplicationStudentId);
        Assert.Equal("CASE-002", captured.CaseRefs[1].SummerEbtCaseId);
        Assert.Null(captured.CaseRefs[1].ApplicationId);
        Assert.Null(captured.CaseRefs[1].ApplicationStudentId);
    }

    [Theory]
    [MemberData(nameof(AllCoreCardReplacementReasons))]
    public async Task RequestCardReplacementAsync_MapsEveryReasonByName(
        CardReplacementReason coreReason)
    {
        PluginCardReplacementRequest? captured = null;
        _plugin.RequestCardReplacementAsync(
                Arg.Do<PluginCardReplacementRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.Success());

        await _sut.RequestCardReplacementAsync(MinimalRequest(coreReason));

        Assert.NotNull(captured);
        Assert.Equal(coreReason.ToString(), captured.Reason.ToString());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_MapsSuccessResultToCore()
    {
        _plugin.RequestCardReplacementAsync(
                Arg.Any<PluginCardReplacementRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.Success());

        var result = await _sut.RequestCardReplacementAsync(MinimalRequest());

        Assert.True(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_MapsPolicyRejectionResultToCore()
    {
        _plugin.RequestCardReplacementAsync(
                Arg.Any<PluginCardReplacementRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.PolicyRejected("CARD_IN_FLIGHT", "A card is already on the way."));

        var result = await _sut.RequestCardReplacementAsync(MinimalRequest());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CARD_IN_FLIGHT", result.ErrorCode);
        Assert.Equal("A card is already on the way.", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_MapsBackendErrorResultToCore()
    {
        _plugin.RequestCardReplacementAsync(
                Arg.Any<PluginCardReplacementRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.BackendError("SP_FAILURE", "Stored procedure failed."));

        var result = await _sut.RequestCardReplacementAsync(MinimalRequest());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("SP_FAILURE", result.ErrorCode);
        Assert.Equal("Stored procedure failed.", result.ErrorMessage);
    }

    private static CardReplacementRequest MinimalRequest(
        CardReplacementReason reason = CardReplacementReason.Unspecified) =>
        new()
        {
            HouseholdIdentifierValue = "guardian@example.com",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "CASE-001" }],
            Reason = reason
        };
}
