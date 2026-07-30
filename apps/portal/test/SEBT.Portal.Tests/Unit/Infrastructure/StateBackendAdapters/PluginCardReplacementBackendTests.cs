using NSubstitute;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackendAdapters;

/// <summary>
/// Pins the adapter boundary: the Core port's opaque case tokens are decoded so
/// the plugin contract receives exactly what it received before the port existed —
/// raw routing triples, the shared household identifier, and a constant
/// Unspecified reason, in ONE batched call.
/// </summary>
public class PluginCardReplacementBackendTests
{
    private readonly ICardReplacementService _contractService =
        Substitute.For<ICardReplacementService>();

    private PluginCardReplacementBackend CreateBackend() => new(_contractService);

    private static string ComposeToken(
        string caseId,
        string householdIdentifier = "user@example.com",
        string? applicationId = null,
        string? applicationStudentId = null)
    {
        var fields = new Dictionary<string, string> { ["caseId"] = caseId };
        if (applicationId != null)
        {
            fields["applicationId"] = applicationId;
        }
        if (applicationStudentId != null)
        {
            fields["applicationStudentId"] = applicationStudentId;
        }
        fields["householdIdentifier"] = householdIdentifier;
        return OpaqueCaseId.Compose(fields);
    }

    public PluginCardReplacementBackendTests()
    {
        _contractService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.Success());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_DecodesTokens_IntoOneBatchedContractCall()
    {
        var backend = CreateBackend();
        var request = new CardReplacementRequest(new List<string>
        {
            ComposeToken("STATE-CASE-1", applicationId: "APP-1", applicationStudentId: "STU-1"),
            ComposeToken("STATE-CASE-2", applicationId: "APP-2", applicationStudentId: "STU-2"),
        });

        var result = await backend.RequestCardReplacementAsync(request);

        Assert.True(result.IsSuccess);
        // The contract sees DECODED raw routing values — never the tokens — plus
        // the single household identifier the tokens share and a constant
        // Unspecified reason, in exactly one call.
        await _contractService.Received(1).RequestCardReplacementAsync(
            Arg.Is<PluginCardReplacementRequest>(r =>
                r.HouseholdIdentifierValue == "user@example.com" &&
                r.Reason == PluginCardReplacementReason.Unspecified &&
                r.CaseRefs.Count == 2 &&
                r.CaseRefs[0].SummerEbtCaseId == "STATE-CASE-1" &&
                r.CaseRefs[0].ApplicationId == "APP-1" &&
                r.CaseRefs[0].ApplicationStudentId == "STU-1" &&
                r.CaseRefs[1].SummerEbtCaseId == "STATE-CASE-2" &&
                r.CaseRefs[1].ApplicationId == "APP-2" &&
                r.CaseRefs[1].ApplicationStudentId == "STU-2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_OmitsApplicationIds_WhenTokensCarryNone()
    {
        var backend = CreateBackend();
        var request = new CardReplacementRequest(new List<string> { ComposeToken("STATE-CASE-1") });

        await backend.RequestCardReplacementAsync(request);

        // Auto-eligible cases have no application identifiers; the contract gets nulls.
        await _contractService.Received(1).RequestCardReplacementAsync(
            Arg.Is<PluginCardReplacementRequest>(r =>
                r.CaseRefs.Count == 1 &&
                r.CaseRefs[0].SummerEbtCaseId == "STATE-CASE-1" &&
                r.CaseRefs[0].ApplicationId == null &&
                r.CaseRefs[0].ApplicationStudentId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FailsLoud_WhenTokensDisagreeOnHouseholdIdentifier()
    {
        var backend = CreateBackend();
        var request = new CardReplacementRequest(new List<string>
        {
            ComposeToken("STATE-CASE-1", householdIdentifier: "user@example.com"),
            ComposeToken("STATE-CASE-2", householdIdentifier: "other@example.com"),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.RequestCardReplacementAsync(request));
        await _contractService.DidNotReceiveWithAnyArgs()
            .RequestCardReplacementAsync(default!, default);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FailsLoud_WhenTokenLacksHouseholdIdentifier()
    {
        var backend = CreateBackend();
        var request = new CardReplacementRequest(new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string> { ["caseId"] = "STATE-CASE-1" }),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.RequestCardReplacementAsync(request));
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FailsLoud_WhenTokenLacksCaseId()
    {
        var backend = CreateBackend();
        var request = new CardReplacementRequest(new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["householdIdentifier"] = "user@example.com",
            }),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.RequestCardReplacementAsync(request));
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FailsLoud_OnEmptyCaseIds()
    {
        var backend = CreateBackend();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.RequestCardReplacementAsync(new CardReplacementRequest(new List<string>())));
    }

    [Fact]
    public async Task RequestCardReplacementAsync_MapsPolicyRejection_PreservingCodeAndMessage()
    {
        _contractService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.PolicyRejected("INELIGIBLE", "Not allowed right now."));
        var backend = CreateBackend();

        var result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { ComposeToken("STATE-CASE-1") }));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INELIGIBLE", result.ErrorCode);
        Assert.Equal("Not allowed right now.", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_MapsBackendError_PreservingCodeAndMessage()
    {
        _contractService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.BackendError("UPSTREAM_500", "Downstream broke."));
        var backend = CreateBackend();

        var result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { ComposeToken("STATE-CASE-1") }));

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("UPSTREAM_500", result.ErrorCode);
        Assert.Equal("Downstream broke.", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_PreservesNullErrorFields()
    {
        // The contract's fields are nullable; the map must not invent values the
        // handler would then surface to the API response.
        _contractService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PluginCardReplacementResult { IsSuccess = false, IsPolicyRejection = false });
        var backend = CreateBackend();

        var result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { ComposeToken("STATE-CASE-1") }));

        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_PassesCancellationTokenToContract()
    {
        var backend = CreateBackend();
        using var cts = new CancellationTokenSource();

        await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { ComposeToken("STATE-CASE-1") }), cts.Token);

        await _contractService.Received(1).RequestCardReplacementAsync(
            Arg.Any<PluginCardReplacementRequest>(), cts.Token);
    }
}
