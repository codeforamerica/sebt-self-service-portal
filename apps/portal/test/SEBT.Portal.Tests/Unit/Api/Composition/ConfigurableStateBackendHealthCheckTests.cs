using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class ConfigurableStateBackendHealthCheckTests
{
    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly IStateBackendHealth _backend = Substitute.For<IStateBackendHealth>();

    [Fact]
    public async Task CheckHealthAsync_WhenFlagOff_IsHealthyWithoutProbing()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(false);
        var sut = new ConfigurableStateBackendHealthCheck(_features, _backend);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        await _backend.DidNotReceive().GetHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFlagOnAndBackendUnhealthy_IsUnhealthy()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _backend.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(new StateBackendHealth(false));
        var sut = new ConfigurableStateBackendHealthCheck(_features, _backend);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFlagOnWithoutBackend_IsUnhealthy()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        var sut = new ConfigurableStateBackendHealthCheck(_features, backend: null);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("ConfigPath", result.Description);
    }
}
