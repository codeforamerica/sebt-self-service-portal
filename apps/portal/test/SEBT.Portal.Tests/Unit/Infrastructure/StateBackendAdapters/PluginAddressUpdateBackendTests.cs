using NSubstitute;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackendAdapters;

/// <summary>
/// Pins the adapter boundary: address updates are household-routed by the request's
/// envelope identifier, so the contract receives ONE call built from the envelope
/// and the five address scalars — with or without case tokens. Tokens, when present,
/// are decoded only to cross-check that they agree with the envelope (fail loud on
/// absence or disagreement).
/// </summary>
public class PluginAddressUpdateBackendTests
{
    private const string HouseholdIdentifier = "user@example.com";

    private readonly IAddressUpdateService _contractService =
        Substitute.For<IAddressUpdateService>();

    private PluginAddressUpdateBackend CreateBackend() => new(_contractService);

    private static string ComposeToken(
        string caseId,
        string householdIdentifier = HouseholdIdentifier)
    {
        return OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = caseId,
            ["householdIdentifier"] = householdIdentifier,
        });
    }

    private static AddressUpdateAddress SampleAddress() => new()
    {
        Line1 = "123 Main St NW",
        Line2 = "Apt 4B",
        City = "Washington",
        State = "District of Columbia",
        Zip = "20001",
    };

    private static AddressUpdateRequest CreateRequest(params string[] caseIds) =>
        new(HouseholdIdentifier, caseIds.ToList(), SampleAddress());

    public PluginAddressUpdateBackendTests()
    {
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
    }

    [Fact]
    public async Task UpdateAddressAsync_BuildsOneContractCall_FromEnvelopeIdentifierAndAddressScalars()
    {
        var backend = CreateBackend();
        var request = CreateRequest(ComposeToken("STATE-CASE-1"), ComposeToken("STATE-CASE-2"));

        var result = await backend.UpdateAddressAsync(request);

        Assert.True(result.IsSuccess);
        // The contract sees the ENVELOPE identifier — never the tokens — and the
        // five address scalars, in exactly one call.
        await _contractService.Received(1).UpdateAddressAsync(
            Arg.Is<PluginAddressUpdateRequest>(r =>
                r.HouseholdIdentifierValue == HouseholdIdentifier &&
                r.Address.StreetAddress1 == "123 Main St NW" &&
                r.Address.StreetAddress2 == "Apt 4B" &&
                r.Address.City == "Washington" &&
                r.Address.State == "District of Columbia" &&
                r.Address.PostalCode == "20001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddressAsync_EmptyCaseIds_IsValid_AndStillDispatches()
    {
        // Household-routed: a zero-case household still updates its address.
        var backend = CreateBackend();

        var result = await backend.UpdateAddressAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        await _contractService.Received(1).UpdateAddressAsync(
            Arg.Is<PluginAddressUpdateRequest>(r =>
                r.HouseholdIdentifierValue == HouseholdIdentifier),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddressAsync_FailsLoud_WhenTokenDisagreesWithEnvelopeIdentifier()
    {
        var backend = CreateBackend();
        var request = CreateRequest(
            ComposeToken("STATE-CASE-1"),
            ComposeToken("STATE-CASE-2", householdIdentifier: "other@example.com"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.UpdateAddressAsync(request));
        await _contractService.DidNotReceiveWithAnyArgs()
            .UpdateAddressAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAddressAsync_FailsLoud_WhenTokenLacksHouseholdIdentifier()
    {
        var backend = CreateBackend();
        var request = CreateRequest(
            OpaqueCaseId.Compose(new Dictionary<string, string> { ["caseId"] = "STATE-CASE-1" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backend.UpdateAddressAsync(request));
        await _contractService.DidNotReceiveWithAnyArgs()
            .UpdateAddressAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAddressAsync_MapsPolicyRejection_PreservingCodeAndMessage()
    {
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.PolicyRejected("HOUSEHOLD_NOT_ELIGIBLE", "Not eligible."));
        var backend = CreateBackend();

        var result = await backend.UpdateAddressAsync(CreateRequest(ComposeToken("STATE-CASE-1")));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("HOUSEHOLD_NOT_ELIGIBLE", result.ErrorCode);
        Assert.Equal("Not eligible.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAddressAsync_MapsBackendError_PreservingCodeAndMessage()
    {
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.BackendError("UPSTREAM_500", "Downstream broke."));
        var backend = CreateBackend();

        var result = await backend.UpdateAddressAsync(CreateRequest(ComposeToken("STATE-CASE-1")));

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("UPSTREAM_500", result.ErrorCode);
        Assert.Equal("Downstream broke.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAddressAsync_PreservesNullErrorFields()
    {
        // The contract's fields are nullable; the map must not invent values the
        // handler would then surface to the API response.
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PluginAddressUpdateResult { IsSuccess = false, IsPolicyRejection = false });
        var backend = CreateBackend();

        var result = await backend.UpdateAddressAsync(CreateRequest(ComposeToken("STATE-CASE-1")));

        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAddressAsync_PassesCancellationTokenToContract()
    {
        var backend = CreateBackend();
        using var cts = new CancellationTokenSource();

        await backend.UpdateAddressAsync(CreateRequest(ComposeToken("STATE-CASE-1")), cts.Token);

        await _contractService.Received(1).UpdateAddressAsync(
            Arg.Any<PluginAddressUpdateRequest>(), cts.Token);
    }
}
