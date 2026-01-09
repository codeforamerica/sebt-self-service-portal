using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using NSubstitute;
using NSubstitute.Core;
using SEBT.Portal.Api.Controllers;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class FeaturesControllerTests
{
    private readonly FeatureFlagQueryService _featureFlagQueryService;
    private readonly FeaturesController _controller;

    public FeaturesControllerTests()
    {
        var featureManager = Substitute.For<IFeatureManager>();
        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        _featureFlagQueryService = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        _controller = new FeaturesController(_featureFlagQueryService);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagsExist_ShouldReturnOkWithFlags()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        var featureNames = new[] { "feature1", "feature2" };
        featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        featureManager.IsEnabledAsync("feature1").Returns(true);
        featureManager.IsEnabledAsync("feature2").Returns(false);

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Equal(2, returnedFlags.Count);
        Assert.True(returnedFlags["feature1"]);
        Assert.False(returnedFlags["feature2"]);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenNoFlagsConfigured_ShouldReturnOkWithEmptyDictionary()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        featureManager.GetFeatureNamesAsync().Returns(Array.Empty<string>().ToAsyncEnumerable());

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Empty(returnedFlags);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        var featureNames = new[] { "enabled_feature" };
        featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        featureManager.IsEnabledAsync("enabled_feature").Returns(true);

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.True(returnedFlags["enabled_feature"]);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagIsDisabled_ShouldReturnFalse()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        var featureNames = new[] { "disabled_feature" };
        featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        featureManager.IsEnabledAsync("disabled_feature").Returns(false);

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags["disabled_feature"]);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenUnknownFlagNotConfigured_ShouldNotIncludeInResponse()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        var featureNames = new[] { "configured_feature" };
        featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        featureManager.IsEnabledAsync("configured_feature").Returns(true);

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags.ContainsKey("unknown_feature"));
        Assert.True(returnedFlags.ContainsKey("configured_feature"));
    }

    [Fact]
    public async Task GetFeatureFlags_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        featureManager.When(x => x.GetFeatureNamesAsync()).Do(_ => throw new Exception("Test exception"));

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenCancelled_ShouldReturnClientClosedRequest()
    {
        // Arrange
        var featureManager = Substitute.For<IFeatureManager>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Return an async enumerable that will throw when enumerated with cancellation
        async IAsyncEnumerable<string> CancelledEnumerable()
        {
            cts.Token.ThrowIfCancellationRequested();
            yield break;
        }
        featureManager.GetFeatureNamesAsync().Returns(CancelledEnumerable());

        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var logger = NullLogger<FeatureFlagQueryService>.Instance;
        var service = new FeatureFlagQueryService(featureManager, configuration, defaultFlagsOptions, logger);
        var controller = new FeaturesController(service);

        // Act
        var result = await controller.GetFeatureFlags(cts.Token);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(499, statusCodeResult.StatusCode); // ClientClosedRequest
    }
}
