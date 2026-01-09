using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class FeaturesControllerTests
{
    private readonly IFeatureFlagService _featureFlagService = Substitute.For<IFeatureFlagService>();
    private readonly FeaturesController _controller;

    public FeaturesControllerTests()
    {
        _controller = new FeaturesController(_featureFlagService);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagsExist_ShouldReturnOkWithFlags()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "feature1", true },
            { "feature2", false }
        };
        _featureFlagService.GetFeatureFlagsAsync().Returns(flags);

        // Act
        var result = await _controller.GetFeatureFlags();

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
        var flags = new Dictionary<string, bool>();
        _featureFlagService.GetFeatureFlagsAsync().Returns(flags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Empty(returnedFlags);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "enabled_feature", true }
        };
        _featureFlagService.GetFeatureFlagsAsync().Returns(flags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.True(returnedFlags["enabled_feature"]);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagIsDisabled_ShouldReturnFalse()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "disabled_feature", false }
        };
        _featureFlagService.GetFeatureFlagsAsync().Returns(flags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags["disabled_feature"]);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenUnknownFlagNotConfigured_ShouldNotIncludeInResponse()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "configured_feature", true }
        };
        _featureFlagService.GetFeatureFlagsAsync().Returns(flags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags.ContainsKey("unknown_feature"));
        Assert.True(returnedFlags.ContainsKey("configured_feature"));
    }
}
