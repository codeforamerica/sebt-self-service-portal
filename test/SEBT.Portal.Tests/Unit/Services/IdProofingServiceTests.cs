using Microsoft.Extensions.Logging;
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

    // --- OnChange hot-reload tests ---

    [Fact]
    public void OnChange_ValidConfig_UpdatesSettings()
    {
        var initialSettings = DefaultSettings();
        var updatedSettings = new IdProofingRequirementsSettings();
        updatedSettings.Requirements["address+write"] = IalRequirement.Uniform(IalLevel.IAL1);

        var monitor = Substitute.For<IOptionsMonitor<IdProofingRequirementsSettings>>();
        monitor.CurrentValue.Returns(initialSettings);

        // Capture the OnChange callback so we can invoke it
        Action<IdProofingRequirementsSettings, string?>? capturedCallback = null;
        monitor.OnChange(Arg.Do<Action<IdProofingRequirementsSettings, string?>>(cb => capturedCallback = cb));

        var service = new IdProofingService(monitor, NullLogger<IdProofingService>.Instance);

        // Verify initial behavior (IAL1plus for address+write)
        var before = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.False(before.IsAllowed);

        // Simulate config change — monitor now returns updated settings
        monitor.CurrentValue.Returns(updatedSettings);
        capturedCallback?.Invoke(updatedSettings, null);

        // After reload, address+write defaults to IAL1plus (unconfigured key → default)
        // But we set it to IAL1 explicitly, so IAL1 user should now be allowed
        var after = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.True(after.IsAllowed);
    }

    [Fact]
    public void OnChange_ValidationFailure_RetainsPreviousSettings()
    {
        var initialSettings = DefaultSettings();
        var monitor = Substitute.For<IOptionsMonitor<IdProofingRequirementsSettings>>();
        monitor.CurrentValue.Returns(initialSettings);

        Action<IdProofingRequirementsSettings, string?>? capturedCallback = null;
        monitor.OnChange(Arg.Do<Action<IdProofingRequirementsSettings, string?>>(cb => capturedCallback = cb));

        var logger = Substitute.For<ILogger<IdProofingService>>();
        var service = new IdProofingService(monitor, logger);

        // Simulate config change that fails validation — CurrentValue throws
        monitor.CurrentValue.Returns(_ =>
            throw new OptionsValidationException(
                "IdProofingRequirementsSettings",
                typeof(IdProofingRequirementsSettings),
                ["write below view"]));

        capturedCallback?.Invoke(initialSettings, null);

        // Service should still use the original settings (IAL1plus for address+write)
        var decision = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.False(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);

        // Verify Critical log was emitted
        logger.Received().Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("config change rejected")),
            Arg.Any<OptionsValidationException>(),
            Arg.Any<Func<object, Exception?, string>>());
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
