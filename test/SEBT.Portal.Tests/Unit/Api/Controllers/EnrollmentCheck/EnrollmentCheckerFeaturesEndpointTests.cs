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
    private readonly EnrollmentCheckController _controller = new();

    public EnrollmentCheckerFeaturesEndpointTests()
    {
        _settings.CurrentValue.Returns(new EnrollmentCheckerSettings
        {
            MaintenanceBanner = new MaintenanceBannerSettings { Message = DefaultMessage }
        });
    }

    [Fact]
    public async Task GetFeatures_WhenBannerFlagEnabled_ReturnsEnabledWithMessage()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(true);

        var result = await _controller.GetFeatures(_featureManager, _settings);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<EnrollmentCheckerFeaturesResponse>(okResult.Value);
        Assert.True(response.MaintenanceBanner.Enabled);
        Assert.Equal(DefaultMessage, response.MaintenanceBanner.Message);
    }

    [Fact]
    public async Task GetFeatures_WhenBannerFlagDisabled_ReturnsDisabledWithMessage()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(false);

        var result = await _controller.GetFeatures(_featureManager, _settings);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<EnrollmentCheckerFeaturesResponse>(okResult.Value);
        Assert.False(response.MaintenanceBanner.Enabled);
        Assert.Equal(DefaultMessage, response.MaintenanceBanner.Message);
    }

    [Fact]
    public async Task GetFeatures_ReadsSettingsOnEachRequest_SoHotReloadedValuesApply()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner).Returns(true);

        var firstResult = await _controller.GetFeatures(_featureManager, _settings);

        var updatedMessage = new Dictionary<string, string>
        {
            ["en"] = "The enrollment checker is undergoing extended maintenance.",
            ["es"] = "El verificador de inscripción está en mantenimiento prolongado.",
        };
        _settings.CurrentValue.Returns(new EnrollmentCheckerSettings
        {
            MaintenanceBanner = new MaintenanceBannerSettings { Message = updatedMessage }
        });

        var secondResult = await _controller.GetFeatures(_featureManager, _settings);

        var firstResponse = Assert.IsType<EnrollmentCheckerFeaturesResponse>(
            Assert.IsType<OkObjectResult>(firstResult).Value);
        var secondResponse = Assert.IsType<EnrollmentCheckerFeaturesResponse>(
            Assert.IsType<OkObjectResult>(secondResult).Value);
        Assert.Equal(DefaultMessage, firstResponse.MaintenanceBanner.Message);
        Assert.Equal(updatedMessage, secondResponse.MaintenanceBanner.Message);
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
