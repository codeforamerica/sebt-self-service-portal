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
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.EnrollmentCheck;

public class EnrollmentCheckerFeaturesEndpointTests
{
    private static readonly Dictionary<string, string> DefaultMessage = new()
    {
        ["en"] = "The enrollment checker may be temporarily unavailable due to system maintenance.",
        ["es"] = "El verificador de inscripción puede no estar disponible temporalmente debido a mantenimiento del sistema.",
    };

    private static readonly IncomeEligibilitySettings DefaultIncomeEligibility = new()
    {
        BaseThreshold = 28953m,
        PerMemberIncrement = 10175m,
        MaxHouseholdSize = 8,
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
            MaintenanceBanner = new MaintenanceBannerSettings { Message = DefaultMessage },
            IncomeEligibility = DefaultIncomeEligibility,
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
    public async Task GetFeatures_WhenIncomeEligibilityFlagEnabled_ReturnsConfiguredThresholds()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(true);

        var response = AssertOkResponse(await GetFeatures());

        Assert.NotNull(response.IncomeEligibility);
        Assert.Equal(28953m, response.IncomeEligibility.BaseThreshold);
        Assert.Equal(10175m, response.IncomeEligibility.PerMemberIncrement);
        Assert.Equal(8, response.IncomeEligibility.MaxHouseholdSize);
    }

    private void UseIncomeEligibility(IncomeEligibilitySettings incomeEligibility) =>
        _settings.CurrentValue.Returns(new EnrollmentCheckerSettings
        {
            MaintenanceBanner = new MaintenanceBannerSettings { Message = DefaultMessage },
            IncomeEligibility = incomeEligibility,
        });

    [Fact]
    public async Task GetFeatures_WhenFlagEnabledButFiguresZeroed_OmitsThresholds()
    {
        // The shipped defaults are zeroes. Serving them would screen every household
        // against $0 and offer a size selector with no options.
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(true);
        UseIncomeEligibility(new IncomeEligibilitySettings());

        var response = AssertOkResponse(await GetFeatures());

        Assert.Null(response.IncomeEligibility);
    }

    [Fact]
    public async Task GetFeatures_WhenFlagEnabledButMaxHouseholdSizeZero_OmitsThresholds()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(true);
        UseIncomeEligibility(new IncomeEligibilitySettings
        {
            BaseThreshold = 28953m,
            PerMemberIncrement = 10175m,
            MaxHouseholdSize = 0,
        });

        var response = AssertOkResponse(await GetFeatures());

        Assert.Null(response.IncomeEligibility);
    }

    [Fact]
    public async Task GetFeatures_WhenFlagEnabledButBaseThresholdZero_OmitsThresholds()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(true);
        UseIncomeEligibility(new IncomeEligibilitySettings
        {
            BaseThreshold = 0m,
            PerMemberIncrement = 10175m,
            MaxHouseholdSize = 8,
        });

        var response = AssertOkResponse(await GetFeatures());

        Assert.Null(response.IncomeEligibility);
    }

    [Fact]
    public async Task GetFeatures_WhenFlagEnabledAndIncrementZero_ReturnsThresholds()
    {
        // A flat threshold that does not rise with household size is a valid
        // configuration, so only the base and the size cap gate the figures.
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(true);
        UseIncomeEligibility(new IncomeEligibilitySettings
        {
            BaseThreshold = 28953m,
            PerMemberIncrement = 0m,
            MaxHouseholdSize = 8,
        });

        var response = AssertOkResponse(await GetFeatures());

        Assert.NotNull(response.IncomeEligibility);
        Assert.Equal(0m, response.IncomeEligibility.PerMemberIncrement);
    }

    [Fact]
    public async Task GetFeatures_WhenIncomeEligibilityFlagDisabled_OmitsThresholds()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(false);

        var response = AssertOkResponse(await GetFeatures());

        // Null rather than zeroed: the checker withdraws the tool instead of
        // screening every household against a $0 threshold.
        Assert.Null(response.IncomeEligibility);
    }

    [Fact]
    public async Task GetFeatures_IncomeEligibilityReadsCurrentValue_SoAppConfigReloadsApply()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility).Returns(true);
        _settings.CurrentValue.Returns(new EnrollmentCheckerSettings
        {
            MaintenanceBanner = new MaintenanceBannerSettings { Message = DefaultMessage },
            IncomeEligibility = new IncomeEligibilitySettings
            {
                BaseThreshold = 30000m,
                PerMemberIncrement = 11000m,
                MaxHouseholdSize = 10,
            },
        });

        var response = AssertOkResponse(await GetFeatures());

        Assert.NotNull(response.IncomeEligibility);
        Assert.Equal(30000m, response.IncomeEligibility.BaseThreshold);
        Assert.Equal(10, response.IncomeEligibility.MaxHouseholdSize);
    }

    [Fact]
    public async Task GetFeatures_WhenApplyFlagEnabled_ReportsApplicationsOpen()
    {
        _featureManager.IsEnabledAsync(FeatureFlags.EnableApply).Returns(true);

        var response = AssertOkResponse(await GetFeatures());

        Assert.True(response.Apply.Enabled);
    }

    [Fact]
    public async Task GetFeatures_WhenApplyFlagUnset_ReportsApplicationsClosed()
    {
        // IFeatureManager returns false for a flag nobody configured, and the checker
        // hides its apply UI on false — the safe direction once the window has ended.
        var response = AssertOkResponse(await GetFeatures());

        Assert.False(response.Apply.Enabled);
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
