using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Controllers;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class OtpControllerTests
{
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly OtpController _controller;

    public OtpControllerTests()
    {
        var logger = NullLogger<OtpController>.Instance;
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });
        _controller = new OtpController(logger, jwtSettings, _hostEnvironment, _featureManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task RequestOtp_WhenSuccess_ReturnsCreated()
    {
        // Arrange
        var command = new RequestOtpApiRequest("user@example.com");
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(Arg.Any<RequestOtpCommand>())
            .Returns(Result.Success());

        // Act
        var result = await _controller.RequestOtp(command, handlerMock);

        // Assert
        Assert.IsType<CreatedResult>(result);
    }

    [Fact]
    public async Task RequestOtp_WhenCommandIsNull_ReturnsBadRequest()
    {
        // Arrange
        RequestOtpApiRequest? command = null!;
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();

        // Act
        var result = await _controller.RequestOtp(command!, handlerMock);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Verify handler is not called when command is null
        await handlerMock.DidNotReceive().Handle(Arg.Any<RequestOtpCommand>());
    }

    [Fact]
    public async Task RequestOtp_WhenFailure_ReturnsBadRequest()
    {
        // Arrange
        var command = new RequestOtpApiRequest("user@example.com");
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(Arg.Any<RequestOtpCommand>())
            .Returns(Result.ValidationFailed("message", "Invalid OTP"));

        // Act
        var result = await _controller.RequestOtp(command, handlerMock);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task ValidateOtp_WhenSuccess_ReturnsNoContentAndSetsAuthCookie()
    {
        // Arrange
        var command = new ValidateOtpApiRequest("user@example.com", "123456");
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        var expectedToken = "test.jwt.token";
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>())
            .Returns(Result<string>.Success(expectedToken));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.IsType<NoContentResult>(result);
        AssertAuthCookieSet(expectedToken);

        await handlerMock.Received(1).Handle(Arg.Any<ValidateOtpCommand>());
    }

    private void AssertAuthCookieSet(string expectedToken)
    {
        var setCookieHeaders = _controller.Response.Headers["Set-Cookie"].ToArray();
        var authCookie = Array.Find(setCookieHeaders, h =>
            h != null && h.StartsWith($"{AuthCookies.AuthCookieName}="));
        Assert.NotNull(authCookie);
        Assert.Contains(expectedToken, authCookie);
        Assert.Contains("httponly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateOtp_WhenFailure_ReturnsBadRequest()
    {
        // Arrange
        var command = new ValidateOtpApiRequest("user@example.com", "123456");
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>())
            .Returns(Result<string>.ValidationFailed("message", "Invalid OTP"));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("The operation failed due to validation", badRequestResult?.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task ValidateOtp_WhenSuccess_DoesNotCallJwtServiceIfHandlerFails()
    {
        // Arrange
        var command = new ValidateOtpApiRequest("user@example.com", "123456");
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>())
            .Returns(Result<string>.ValidationFailed("Otp", "Invalid OTP"));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ValidateOtp_WhenCommandIsNull_ReturnsBadRequest()
    {
        // Arrange
        ValidateOtpApiRequest? command = null!;
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();

        // Act
        var result = await _controller.ValidateOtp(command!, handlerMock);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Verify handler is not called when command is null
        await handlerMock.DidNotReceive().Handle(Arg.Any<ValidateOtpCommand>());
    }

    [Fact]
    public async Task ValidateOtp_WhenJwtTokenGenerationFails_ReturnsInternalServerError()
    {
        // Arrange
        var command = new ValidateOtpApiRequest("user@example.com", "123456");
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>())
            .Returns(Result<string>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "An error occurred while generating the authentication token."));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
        Assert.Contains("error occurred while generating", badRequestResult.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateOtp_WhenFailure_ReturnsErrorInCorrectFormat()
    {
        // Arrange
        var command = new ValidateOtpApiRequest("user@example.com", "123456");
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>())
            .Returns(Result<string>.ValidationFailed("Otp", "Invalid OTP"));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        var errorProperty = badRequestResult.Value.GetType().GetProperty("Error");
        Assert.NotNull(errorProperty);
        var errorValue = errorProperty.GetValue(badRequestResult.Value);
        Assert.NotNull(errorValue);

        Assert.Contains("validation errors", errorValue.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    #region RequestOtp bypass decision tests

    [Fact]
    public async Task RequestOtp_WhenBypassEnabled_AndStaging_AndMatchingEmail_SetsCommandBypassTrue()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(true);
        _hostEnvironment.EnvironmentName.Returns("Staging");

        var request = new RequestOtpApiRequest(OtpBypassSettings.Email);
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(Arg.Any<RequestOtpCommand>()).Returns(Result.Success());

        // Act
        await _controller.RequestOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RequestOtpCommand>(c => c.BypassOtp == true));
    }

    [Fact]
    public async Task RequestOtp_WhenBypassEnabled_ButProduction_SetsCommandBypassFalse()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(true);
        _hostEnvironment.EnvironmentName.Returns("Production");

        var request = new RequestOtpApiRequest(OtpBypassSettings.Email);
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(Arg.Any<RequestOtpCommand>()).Returns(Result.Success());

        // Act
        await _controller.RequestOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RequestOtpCommand>(c => c.BypassOtp == false));
    }

    [Fact]
    public async Task RequestOtp_WhenBypassEnabled_AndStaging_ButWrongEmail_SetsCommandBypassFalse()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(true);
        _hostEnvironment.EnvironmentName.Returns("Staging");

        var request = new RequestOtpApiRequest("other@example.com");
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(Arg.Any<RequestOtpCommand>()).Returns(Result.Success());

        // Act
        await _controller.RequestOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RequestOtpCommand>(c => c.BypassOtp == false));
    }

    [Fact]
    public async Task RequestOtp_WhenBypassDisabled_SetsCommandBypassFalse()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(false);
        _hostEnvironment.EnvironmentName.Returns("Staging");

        var request = new RequestOtpApiRequest(OtpBypassSettings.Email);
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(Arg.Any<RequestOtpCommand>()).Returns(Result.Success());

        // Act
        await _controller.RequestOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RequestOtpCommand>(c => c.BypassOtp == false));
    }

    #endregion

    #region ValidateOtp bypass decision tests

    [Fact]
    public async Task ValidateOtp_WhenBypassEnabled_AndStaging_AndMatchingEmail_SetsCommandBypassTrue()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(true);
        _hostEnvironment.EnvironmentName.Returns("Staging");

        var request = new ValidateOtpApiRequest(OtpBypassSettings.Email, OtpBypassSettings.OtpCode);
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>()).Returns(Result<string>.Success("token"));

        // Act
        await _controller.ValidateOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<ValidateOtpCommand>(c => c.BypassOtp == true));
    }

    [Fact]
    public async Task ValidateOtp_WhenBypassEnabled_ButProduction_SetsCommandBypassFalse()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(true);
        _hostEnvironment.EnvironmentName.Returns("Production");

        var request = new ValidateOtpApiRequest(OtpBypassSettings.Email, OtpBypassSettings.OtpCode);
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>()).Returns(Result<string>.Success("token"));

        // Act
        await _controller.ValidateOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<ValidateOtpCommand>(c => c.BypassOtp == false));
    }

    [Fact]
    public async Task ValidateOtp_WhenBypassEnabled_AndStaging_ButWrongEmail_SetsCommandBypassFalse()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(true);
        _hostEnvironment.EnvironmentName.Returns("Staging");

        var request = new ValidateOtpApiRequest("other@example.com", OtpBypassSettings.OtpCode);
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>()).Returns(Result<string>.Success("token"));

        // Act
        await _controller.ValidateOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<ValidateOtpCommand>(c => c.BypassOtp == false));
    }

    [Fact]
    public async Task ValidateOtp_WhenBypassDisabled_SetsCommandBypassFalse()
    {
        // Arrange
        _featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName).Returns(false);
        _hostEnvironment.EnvironmentName.Returns("Staging");

        var request = new ValidateOtpApiRequest(OtpBypassSettings.Email, OtpBypassSettings.OtpCode);
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(Arg.Any<ValidateOtpCommand>()).Returns(Result<string>.Success("token"));

        // Act
        await _controller.ValidateOtp(request, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<ValidateOtpCommand>(c => c.BypassOtp == false));
    }

    #endregion
}
