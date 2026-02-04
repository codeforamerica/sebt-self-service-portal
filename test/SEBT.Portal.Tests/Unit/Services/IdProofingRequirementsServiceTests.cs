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
        var settings = new IdProofingRequirementsSettings { Address = "IAL1", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.Completed);

        Assert.True(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenCompleted_AndAddressRequiresIal1plus_ReturnsIncludeAddressTrue()
    {
        var settings = new IdProofingRequirementsSettings { Address = "IAL1plus", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.Completed);

        Assert.True(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenNotStarted_AndAllRequireIal1_ReturnsAllFalse()
    {
        var settings = new IdProofingRequirementsSettings { Address = "IAL1", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.NotStarted);

        Assert.False(result.IncludeAddress);
        Assert.False(result.IncludeEmail);
        Assert.False(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenAddressRequiresIal2_AndUserCompleted_ReturnsIncludeAddressFalse()
    {
        var settings = new IdProofingRequirementsSettings { Address = "IAL2", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.Completed);

        Assert.False(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenAllRequireIal1_AndUserNotVerified_ReturnsAllFalse()
    {
        var settings = new IdProofingRequirementsSettings { Address = "IAL1", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.NotStarted);

        Assert.False(result.IncludeAddress);
        Assert.False(result.IncludeEmail);
        Assert.False(result.IncludePhone);
    }

    [Fact]
    public void GetPiiVisibility_WhenAllRequireIal1_AndUserCompleted_ReturnsAllTrue()
    {
        var settings = new IdProofingRequirementsSettings { Address = "IAL1", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.Completed);

        Assert.True(result.IncludeAddress);
        Assert.True(result.IncludeEmail);
        Assert.True(result.IncludePhone);
    }

    [Theory]
    [InlineData(IdProofingStatus.InProgress)]
    [InlineData(IdProofingStatus.Failed)]
    [InlineData(IdProofingStatus.Expired)]
    public void GetPiiVisibility_WhenAddressRequiresIal1_AndUserNotCompleted_ReturnsIncludeAddressFalse(IdProofingStatus status)
    {
        var settings = new IdProofingRequirementsSettings { Address = "IAL1", Email = "IAL1", Phone = "IAL1" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(status);

        Assert.False(result.IncludeAddress);
    }

    [Fact]
    public void GetPiiVisibility_WhenUnknownRequirementValue_FailsSafe_ReturnsPiiHidden()
    {
        var settings = new IdProofingRequirementsSettings { Address = "invalid", Email = "IALl", Phone = "IAL3" };
        var service = CreateService(settings);

        var result = service.GetPiiVisibility(IdProofingStatus.Completed);

        Assert.False(result.IncludeAddress);
        Assert.False(result.IncludeEmail);
        Assert.False(result.IncludePhone);
    }
}
