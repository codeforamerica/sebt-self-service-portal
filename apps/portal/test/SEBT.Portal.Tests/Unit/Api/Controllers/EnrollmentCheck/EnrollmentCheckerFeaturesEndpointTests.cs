using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api;
using SEBT.Portal.Api.Controllers.EnrollmentCheck;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.EnrollmentCheck;

public class EnrollmentCheckerFeaturesEndpointTests
{
    private static readonly Dictionary<string, string> DefaultMessage = new()
    {
        ["en"] = "The enrollment checker may be temporarily unavailable due to system maintenance.",
        ["es"] = "El verificador de inscripción puede no estar disponible temporalmente debido a mantenimiento del sistema.",
    };

    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly IOptionsMonitor<EnrollmentCheckerSettings> _settings =
        Substitute.For<IOptionsMonitor<EnrollmentCheckerSettings>>();
    private readonly IOutagePageStateResolver _outagePageStateResolver =
        Substitute.For<IOutagePageStateResolver>();
    private readonly EnrollmentCheckController _controller = new();

    public EnrollmentCheckerFeaturesEndpointTests()
    {
        _settings.CurrentValue.Returns(new EnrollmentCheckerSettings
        {
            MaintenanceBanner = new MaintenanceBannerSettings { Message = DefaultMessage }
        });
    }

    private Task<IActionResult> GetFeatures() =>
        _controller.GetFeatures(_featureManager, _settings, _outagePageStateResolver);

    private static EnrollmentCheckerFeaturesResponse AssertOkResponse(IActionResult result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<EnrollmentCheckerFeaturesResponse>(okResult.Value);
    }

    [Fact]
    public async Task GetFeatures_WhenBannerFlagEnabled_ReturnsEnabledWithMessage()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(true);

        var response = AssertOkResponse(await GetFeatures());

        Assert.True(response.MaintenanceBanner.Enabled);
        Assert.Equal(DefaultMessage, response.MaintenanceBanner.Message);
    }

    [Fact]
    public async Task GetFeatures_WhenBannerFlagDisabled_ReturnsDisabledWithMessage()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(false);

        var response = AssertOkResponse(await GetFeatures());

        Assert.False(response.MaintenanceBanner.Enabled);
        Assert.Equal(DefaultMessage, response.MaintenanceBanner.Message);
    }

    [Fact]
    public async Task GetFeatures_ReadsSettingsOnEachRequest_SoHotReloadedValuesApply()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(true);

        var firstResult = await GetFeatures();

        var updatedMessage = new Dictionary<string, string>
        {
            ["en"] = "The enrollment checker is undergoing extended maintenance.",
            ["es"] = "El verificador de inscripción está en mantenimiento prolongado.",
        };
        _settings.CurrentValue.Returns(new EnrollmentCheckerSettings
        {
            MaintenanceBanner = new MaintenanceBannerSettings { Message = updatedMessage }
        });

        var secondResult = await GetFeatures();

        Assert.Equal(DefaultMessage, AssertOkResponse(firstResult).MaintenanceBanner.Message);
        Assert.Equal(updatedMessage, AssertOkResponse(secondResult).MaintenanceBanner.Message);
    }

    [Fact]
    public async Task GetFeatures_WhenOutagePageActive_ReturnsOutagePageEnabled()
    {
        _outagePageStateResolver.ResolveAsync(OutageTarget.EnrollmentChecker)
            .Returns(new OutagePageState(IsActive: true, ScheduleIsAuthority: false));

        var response = AssertOkResponse(await GetFeatures());

        Assert.True(response.OutagePage.Enabled);
    }

    [Fact]
    public async Task GetFeatures_WhenOutagePageInactive_ReturnsOutagePageDisabled()
    {
        _outagePageStateResolver.ResolveAsync(OutageTarget.EnrollmentChecker)
            .Returns(new OutagePageState(IsActive: false, ScheduleIsAuthority: false));

        var response = AssertOkResponse(await GetFeatures());

        Assert.False(response.OutagePage.Enabled);
    }

    [Fact]
    public async Task GetFeatures_OutageStateDoesNotAffectBannerFields()
    {
        // The outage page is additive; the maintenance banner mechanism stays independent.
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(true);
        _outagePageStateResolver.ResolveAsync(OutageTarget.EnrollmentChecker)
            .Returns(new OutagePageState(IsActive: true, ScheduleIsAuthority: false));

        var response = AssertOkResponse(await GetFeatures());

        Assert.True(response.MaintenanceBanner.Enabled);
        Assert.Equal(DefaultMessage, response.MaintenanceBanner.Message);
        Assert.True(response.OutagePage.Enabled);
    }

    [Fact]
    public void GetFeatures_HasDedicatedRateLimitPolicy()
    {
        var attribute = typeof(EnrollmentCheckController)
            .GetMethod(nameof(EnrollmentCheckController.GetFeatures))!
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RateLimitPolicies.CheckerFeatures, attribute.PolicyName);
        // The features poll must not share the enrollment-check partition: open checker
        // tabs polling once a minute would drain the per-IP budget real checks need.
        Assert.NotEqual(RateLimitPolicies.EnrollmentCheck, attribute.PolicyName);
    }
}
