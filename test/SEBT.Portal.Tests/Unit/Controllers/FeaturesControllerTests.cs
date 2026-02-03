using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers;
using SEBT.Portal.Kernel.Services;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class FeaturesControllerTests
{
    private readonly IFeatureFlagQueryService _featureFlagQueryService;
    private readonly FeaturesController _controller;

    public FeaturesControllerTests()
    {
        _featureFlagQueryService = Substitute.For<IFeatureFlagQueryService>();
        _controller = new FeaturesController(_featureFlagQueryService);
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagsExist_ShouldReturnOkWithFlags()
    {
        // Arrange
        var expectedFlags = new Dictionary<string, bool>
        {
            { "feature1", true },
            { "feature2", false }
        };
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedFlags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Equal(2, returnedFlags.Count);
        Assert.True(returnedFlags["feature1"]);
        Assert.False(returnedFlags["feature2"]);
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureFlags_WhenNoFlagsConfigured_ShouldReturnOkWithEmptyDictionary()
    {
        // Arrange
        var expectedFlags = new Dictionary<string, bool>();
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedFlags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.Empty(returnedFlags);
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        // Arrange
        var expectedFlags = new Dictionary<string, bool>
        {
            { "enabled_feature", true }
        };
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedFlags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.True(returnedFlags["enabled_feature"]);
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureFlags_WhenFlagIsDisabled_ShouldReturnFalse()
    {
        // Arrange
        var expectedFlags = new Dictionary<string, bool>
        {
            { "disabled_feature", false }
        };
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedFlags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags["disabled_feature"]);
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureFlags_WhenUnknownFlagNotConfigured_ShouldNotIncludeInResponse()
    {
        // Arrange
        var expectedFlags = new Dictionary<string, bool>
        {
            { "configured_feature", true }
        };
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedFlags);

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFlags = Assert.IsType<Dictionary<string, bool>>(okResult.Value);
        Assert.False(returnedFlags.ContainsKey("unknown_feature"));
        Assert.True(returnedFlags.ContainsKey("configured_feature"));
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureFlags_WhenServiceThrowsException_ShouldReturnInternalServerError()
    {
        // Arrange
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Dictionary<string, bool>>(new Exception("Test exception")));

        // Act
        var result = await _controller.GetFeatureFlags();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        var errorResponse = Assert.IsType<SEBT.Portal.Api.Models.ErrorResponse>(statusCodeResult.Value);
        Assert.Contains("Failed to retrieve feature flags", errorResponse.Error, StringComparison.OrdinalIgnoreCase);
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureFlags_WhenCancelled_ShouldReturnClientClosedRequest()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _featureFlagQueryService.GetFeatureFlagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Dictionary<string, bool>>(new OperationCanceledException()));

        // Act
        var result = await _controller.GetFeatureFlags(cts.Token);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(499, statusCodeResult.StatusCode); // ClientClosedRequest
        var errorResponse = Assert.IsType<SEBT.Portal.Api.Models.ErrorResponse>(statusCodeResult.Value);
        Assert.Contains("Request was cancelled", errorResponse.Error, StringComparison.OrdinalIgnoreCase);
        await _featureFlagQueryService.Received(1).GetFeatureFlagsAsync(Arg.Any<CancellationToken>());
    }
}
