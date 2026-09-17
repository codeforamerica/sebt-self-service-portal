using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedPluginHealthCheckTests
{
    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly IHealthCheck _inner = Substitute.For<IHealthCheck>();

    [Fact]
    public async Task CheckHealthAsync_WhenFlagOn_IsHealthyWithoutCallingInner()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Unhealthy("plugin down"));
        var sut = new FeatureGatedPluginHealthCheck(_features, () => _inner);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        await _inner.DidNotReceive().CheckHealthAsync(
            Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFlagOff_DelegatesToInner()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(false);
        _inner.CheckHealthAsync(Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>())
            .Returns(HealthCheckResult.Unhealthy("plugin down"));
        var sut = new FeatureGatedPluginHealthCheck(_features, () => _inner);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        await _inner.Received(1).CheckHealthAsync(
            Arg.Any<HealthCheckContext>(), Arg.Any<CancellationToken>());
    }
}
