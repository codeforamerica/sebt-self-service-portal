using NSubstitute;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class AddressValidationServiceAdapterTests
{
    private static Address SampleAddress() => new()
    {
        StreetAddress1 = "123 Main St",
        City = "Washington",
        State = "DC",
        PostalCode = "20001"
    };

    [Fact]
    public async Task ValidateAsync_ReturnsValid_WhenUpdateServiceSucceeds()
    {
        var normalized = new Address
        {
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "DC",
            PostalCode = "20001-1234"
        };

        var service = Substitute.For<IAddressUpdateService>();
        service.ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressUpdateSuccess>.Success(new AddressUpdateSuccess { NormalizedAddress = normalized }));

        var adapter = new AddressValidationServiceAdapter(service);
        var result = await adapter.ValidateAsync(SampleAddress());

        Assert.True(result.IsValid);
        Assert.Equal("123 Main St NW", result.NormalizedAddress?.StreetAddress1);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenUpdateServiceReturnsValidationFailed()
    {
        var service = Substitute.For<IAddressUpdateService>();
        service.ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressUpdateSuccess>.ValidationFailed("address", "This address could not be verified."));

        var adapter = new AddressValidationServiceAdapter(service);
        var result = await adapter.ValidateAsync(SampleAddress());

        Assert.False(result.IsValid);
        Assert.Contains("could not be verified", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenUpdateServiceReturnsDependencyFailed()
    {
        var service = Substitute.For<IAddressUpdateService>();
        service.ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressUpdateSuccess>.DependencyFailed(
                DependencyFailedReason.Timeout,
                "Address verification timed out."));

        var adapter = new AddressValidationServiceAdapter(service);
        var result = await adapter.ValidateAsync(SampleAddress());

        Assert.False(result.IsValid);
        Assert.Contains("timed out", result.ErrorMessage);
    }
}
