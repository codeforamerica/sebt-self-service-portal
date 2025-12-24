using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class OtpControllerTests
{
    private readonly OtpController _controller;

    public OtpControllerTests()
    {
        var logger = NullLogger<OtpController>.Instance;
        _controller = new OtpController(logger);
    }

    [Fact]
    public async Task RequestOtp_WhenSuccess_ReturnsCreated()
    {
        // Arrange
        var command = new RequestOtpCommand();
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(command)
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
        RequestOtpCommand? command = null;
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
        var command = new RequestOtpCommand();
        var handlerMock = Substitute.For<ICommandHandler<RequestOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.ValidationFailed("message", "Invalid OTP"));

        // Act
        var result = await _controller.RequestOtp(command, handlerMock);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task ValidateOtp_CallsHandler()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.Success());
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();
        jwtTokenServiceMock.GenerateToken(Arg.Any<string>()).Returns("test.token");

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock, jwtTokenServiceMock);

        // Assert
        await handlerMock.Received(1).Handle(command);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ValidateOtp_WhenSuccess_ReturnsOkWithJwtToken()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.Success());
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();
        var expectedToken = "test.jwt.token";
        jwtTokenServiceMock.GenerateToken(command.Email).Returns(expectedToken);

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock, jwtTokenServiceMock);

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var response = Assert.IsType<ValidateOtpResponse>(okResult.Value);
        Assert.Equal(expectedToken, response.Token);

        await handlerMock.Received(1).Handle(command);
        jwtTokenServiceMock.Received(1).GenerateToken(command.Email);
    }

    [Fact]
    public async Task ValidateOtp_WhenFailure_ReturnsBadRequest()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.ValidationFailed("message", "Invalid OTP"));
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock, jwtTokenServiceMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("The operation failed due to validation", badRequestResult?.Value?.ToString() ?? string.Empty);

        jwtTokenServiceMock.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task ValidateOtp_WhenSuccess_DoesNotCallJwtServiceIfHandlerFails()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.ValidationFailed("Otp", "Invalid OTP"));
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock, jwtTokenServiceMock);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<BadRequestObjectResult>(result);

        jwtTokenServiceMock.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task ValidateOtp_WhenCommandIsNull_ReturnsBadRequest()
    {
        // Arrange
        ValidateOtpCommand? command = null;
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();

        // Act
        var result = await _controller.ValidateOtp(command!, handlerMock, jwtTokenServiceMock);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Verify handler and JWT service are not called when command is null
        await handlerMock.DidNotReceive().Handle(Arg.Any<ValidateOtpCommand>());
        jwtTokenServiceMock.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task ValidateOtp_WhenJwtTokenGenerationThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.Success());
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();
        jwtTokenServiceMock.GenerateToken(command.Email)
            .Returns(x => throw new InvalidOperationException("JWT configuration error"));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock, jwtTokenServiceMock);

        // Assert
        Assert.NotNull(result);
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        Assert.NotNull(statusCodeResult.Value);

        jwtTokenServiceMock.Received(1).GenerateToken(command.Email);
    }

    [Fact]
    public async Task ValidateOtp_WhenFailure_ReturnsErrorInCorrectFormat()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.ValidationFailed("Otp", "Invalid OTP"));
        var jwtTokenServiceMock = Substitute.For<IJwtTokenService>();

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock, jwtTokenServiceMock);

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
}
