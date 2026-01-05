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
    public void GetFeatureFlags_WhenFlagIsEnabled_ReturnsTrue()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "multi_language", true }
        };
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        var result = _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.True(returnedFlags.ContainsKey("multi_language"));
        Assert.True(returnedFlags["multi_language"]);
    }

    [Fact]
    public void GetFeatureFlags_WhenFlagIsDisabled_ReturnsFalse()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "advanced_search", false }
        };
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        var result = _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.True(returnedFlags.ContainsKey("advanced_search"));
        Assert.False(returnedFlags["advanced_search"]);
    }

    [Fact]
    public void GetFeatureFlags_WhenUnknownFlag_ShouldNotBeIncluded()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "multi_language", true },
            { "advanced_search", false }
        };
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        var result = _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags.ContainsKey("unknown_feature"));
    }

    [Fact]
    public void GetFeatureFlags_ReturnsOkResult()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "multi_language", true },
            { "advanced_search", false },
            { "experimental_ui", true }
        };
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        var result = _controller.GetFeatureFlags();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetFeatureFlags_CallsService()
    {
        // Arrange
        var flags = new Dictionary<string, bool>();
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        _controller.GetFeatureFlags();

        // Assert
        _featureFlagService.Received(1).GetFeatureFlags();
    }

    [Fact]
    public void GetFeatureFlags_WhenEmpty_ReturnsEmptyDictionary()
    {
        // Arrange
        var flags = new Dictionary<string, bool>();
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        var result = _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Empty(returnedFlags);
    }

    [Fact]
    public void GetFeatureFlags_ReturnsAllConfiguredFlags()
    {
        // Arrange
        var flags = new Dictionary<string, bool>
        {
            { "multi_language", true },
            { "advanced_search", false },
            { "experimental_ui", true },
            { "new_feature", false }
        };
        _featureFlagService.GetFeatureFlags().Returns(flags);

        // Act
        var result = _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Equal(4, returnedFlags.Count);
        Assert.True(returnedFlags["multi_language"]);
        Assert.False(returnedFlags["advanced_search"]);
        Assert.True(returnedFlags["experimental_ui"]);
        Assert.False(returnedFlags["new_feature"]);
    }
}

