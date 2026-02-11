using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Services;

public class IdProofingRequirementsServiceTests
{
    private static IdProofingRequirementsService CreateService(IdProofingRequirementsSettings settings) =>
        new(Options.Create(settings), NullLogger<IdProofingRequirementsService>.Instance);

    [Fact]
    public void GetPiiVisibility_WhenCompleted_AndAllRequireIal1_ReturnsAllTrue()
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL1, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.IAL1plus);

        Assert.True(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenCompleted_AndAddressRequiresIal1plus_ReturnsIncludeAddressTrue()
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL1plus, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.IAL1plus);

        Assert.True(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenNotStarted_AndAllRequireIal1_ReturnsAllFalse()
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL1, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.None);

        Assert.False(result.IncludeAddress);
        Assert.False(result.IncludeEmail);
        Assert.False(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenAddressRequiresIal2_AndUserCompleted_ReturnsIncludeAddressFalse()
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL2, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.IAL1plus);

        Assert.False(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenAllRequireIal1_AndUserNotVerified_ReturnsAllFalse()
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL1, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.None);

        Assert.False(result.IncludeAddress);
        Assert.False(result.IncludeEmail);
        Assert.False(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenAllRequireIal1_AndUserCompleted_ReturnsAllTrue()
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL1, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.IAL1plus);

        Assert.True(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Theory]
    [InlineData(UserIalLevel.None)]
    public void GetPiiVisibility_WhenAddressRequiresIal1_AndUserNotCompleted_ReturnsIncludeAddressFalse(UserIalLevel userIalLevel)
    {
        var settings = new IdProofingRequirementsSettings { AddressView = IalLevel.IAL1, EmailView = IalLevel.IAL1, PhoneView = IalLevel.IAL1 };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(userIalLevel);

        Assert.False(result.IncludeAddress);
    }

    [Fact]
    public void GetPiiVisibility_WhenInvalidEnumValue_FailsSafe_ReturnsPiiHidden()
    {
        var settings = new IdProofingRequirementsSettings
        {
            AddressView = (IalLevel)99,
            EmailView = IalLevel.IAL1,
            PhoneView = IalLevel.IAL1
        };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(UserIalLevel.IAL1plus);

        Assert.False(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }
}
