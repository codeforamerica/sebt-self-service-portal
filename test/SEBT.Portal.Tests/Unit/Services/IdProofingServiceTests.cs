using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class IdProofingServiceTests
{
    private static IdProofingService CreateService(IdProofingRequirementsSettings settings)
    {
        var monitor = Substitute.For<IOptionsMonitor<IdProofingRequirementsSettings>>();
        monitor.CurrentValue.Returns(settings);
        return new IdProofingService(monitor, NullLogger<IdProofingService>.Instance);
    }

    private static IdProofingRequirementsSettings DefaultSettings()
    {
        var settings = new IdProofingRequirementsSettings();
        settings.Requirements["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        settings.Requirements["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        settings.Requirements["email+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        settings.Requirements["phone+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        settings.Requirements["household+view"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        settings.Requirements["card+write"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        return settings;
    }

    private static SummerEbtCase ApplicationCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = false,
            IsCoLoaded = false
        };

    // --- Evaluate tests ---

    [Fact]
    public void Evaluate_UserMeetsRequirement_ReturnsAllowed()
    {
        var service = CreateService(DefaultSettings());
        var decision = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1plus, [ApplicationCase()]);
        Assert.True(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);
    }

    [Fact]
    public void Evaluate_UserBelowRequirement_ReturnsDenied()
    {
        var service = CreateService(DefaultSettings());
        var decision = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.False(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);
    }

    [Fact]
    public void Evaluate_UnconfiguredKey_DefaultsToIal1plus()
    {
        var settings = new IdProofingRequirementsSettings();
        var service = CreateService(settings);
        var decision = service.Evaluate(
            ProtectedResource.Card, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.False(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);
    }

    // --- GetVisibility tests ---

    [Fact]
    public void GetVisibility_Ial1plus_ShowsAddress()
    {
        var service = CreateService(DefaultSettings());
        var visibility = service.GetVisibility(UserIalLevel.IAL1plus);
        Assert.True(visibility.IncludeAddress);
        Assert.True(visibility.IncludeEmail);
        Assert.True(visibility.IncludePhone);
    }

    [Fact]
    public void GetVisibility_Ial1_HidesAddressShowsEmailPhone()
    {
        var service = CreateService(DefaultSettings());
        var visibility = service.GetVisibility(UserIalLevel.IAL1);
        Assert.False(visibility.IncludeAddress);
        Assert.True(visibility.IncludeEmail);
        Assert.True(visibility.IncludePhone);
    }

    [Fact]
    public void GetVisibility_None_HidesAll()
    {
        var settings = DefaultSettings();
        settings.Requirements["email+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        settings.Requirements["phone+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        var service = CreateService(settings);

        var visibility = service.GetVisibility(UserIalLevel.None);
        Assert.False(visibility.IncludeAddress);
        Assert.False(visibility.IncludeEmail);
        Assert.False(visibility.IncludePhone);
    }
}
