using NSubstitute;
using SEBT.Portal.Core.StateConnector;
using SEBT.Portal.Infrastructure.StateConnector;
using IPluginAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateConnector;

/// <summary>
/// Verifies that <see cref="PluginAddressUpdateService"/> maps every field between the
/// Core port models and the plugin contract models, in both directions.
/// </summary>
public class PluginAddressUpdateServiceTests
{
    private readonly IPluginAddressUpdateService _plugin =
        Substitute.For<IPluginAddressUpdateService>();

    private readonly PluginAddressUpdateService _sut;

    public PluginAddressUpdateServiceTests()
    {
        _sut = new PluginAddressUpdateService(_plugin);
    }

    [Fact]
    public async Task UpdateAddressAsync_MapsFullyPopulatedRequestToPlugin()
    {
        var request = new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "guardian@example.com",
            Address = new Address
            {
                StreetAddress1 = "123 Main St",
                StreetAddress2 = "Apt 4B",
                City = "Washington",
                State = "DC",
                PostalCode = "20001"
            }
        };

        PluginAddressUpdateRequest? captured = null;
        using var cts = new CancellationTokenSource();
        _plugin.UpdateAddressAsync(
                Arg.Do<PluginAddressUpdateRequest>(r => captured = r),
                cts.Token)
            .Returns(PluginAddressUpdateResult.Success());

        await _sut.UpdateAddressAsync(request, cts.Token);

        Assert.NotNull(captured);
        Assert.Equal("guardian@example.com", captured.HouseholdIdentifierValue);
        Assert.Equal("123 Main St", captured.Address.StreetAddress1);
        Assert.Equal("Apt 4B", captured.Address.StreetAddress2);
        Assert.Equal("Washington", captured.Address.City);
        Assert.Equal("DC", captured.Address.State);
        Assert.Equal("20001", captured.Address.PostalCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_PreservesNullAddressFields()
    {
        var request = new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "guardian@example.com",
            Address = new Address()
        };

        PluginAddressUpdateRequest? captured = null;
        _plugin.UpdateAddressAsync(
                Arg.Do<PluginAddressUpdateRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());

        await _sut.UpdateAddressAsync(request);

        Assert.NotNull(captured);
        Assert.Null(captured.Address.StreetAddress1);
        Assert.Null(captured.Address.StreetAddress2);
        Assert.Null(captured.Address.City);
        Assert.Null(captured.Address.State);
        Assert.Null(captured.Address.PostalCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_MapsSuccessResultToCore()
    {
        _plugin.UpdateAddressAsync(
                Arg.Any<PluginAddressUpdateRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());

        var result = await _sut.UpdateAddressAsync(MinimalRequest());

        Assert.True(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAddressAsync_MapsPolicyRejectionResultToCore()
    {
        _plugin.UpdateAddressAsync(
                Arg.Any<PluginAddressUpdateRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.PolicyRejected("SNAP_HOUSEHOLD", "Contact your case worker."));

        var result = await _sut.UpdateAddressAsync(MinimalRequest());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("SNAP_HOUSEHOLD", result.ErrorCode);
        Assert.Equal("Contact your case worker.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAddressAsync_MapsBackendErrorResultToCore()
    {
        _plugin.UpdateAddressAsync(
                Arg.Any<PluginAddressUpdateRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.BackendError("TIMEOUT", "Backend timed out."));

        var result = await _sut.UpdateAddressAsync(MinimalRequest());

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.Equal("Backend timed out.", result.ErrorMessage);
    }

    private static AddressUpdateRequest MinimalRequest() =>
        new()
        {
            HouseholdIdentifierValue = "guardian@example.com",
            Address = new Address()
        };
}
