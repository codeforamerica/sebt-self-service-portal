using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class PassThroughAddressUpdateServiceTests
{
    private static PassThroughAddressUpdateService CreateService(AddressValidationPolicySettings policy)
    {
        var monitor = Substitute.For<IOptionsMonitor<AddressValidationPolicySettings>>();
        monitor.CurrentValue.Returns(policy);
        return new PassThroughAddressUpdateService(monitor);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_AllowsGeneralDelivery_WhenPolicyAllows()
    {
        var service = CreateService(new AddressValidationPolicySettings { AllowGeneralDelivery = true });

        var request = new AddressUpdateOperationRequest
        {
            StreetAddress1 = "General Delivery",
            City = "Washington",
            State = "DC",
            PostalCode = "20001"
        };

        var result = await service.ValidateAndNormalizeAsync(request);

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.True(success.Value.IsGeneralDelivery);
        Assert.Equal("General Delivery", success.Value.NormalizedAddress.StreetAddress1);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_RejectsGeneralDelivery_WhenPolicyDisallows()
    {
        var service = CreateService(new AddressValidationPolicySettings { AllowGeneralDelivery = false });

        var request = new AddressUpdateOperationRequest
        {
            StreetAddress1 = "General Delivery",
            City = "Washington",
            State = "DC",
            PostalCode = "20001"
        };

        var result = await service.ValidateAndNormalizeAsync(request);

        Assert.IsType<ValidationFailedResult<AddressUpdateSuccess>>(result);
    }
}
