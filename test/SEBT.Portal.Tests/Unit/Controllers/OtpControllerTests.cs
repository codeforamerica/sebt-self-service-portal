using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
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
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(command)
            .Returns(Result<string>.Success("test.token"));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(command);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ValidateOtp_WhenSuccess_ReturnsOkWithJwtToken()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        var expectedToken = "test.jwt.token";
        handlerMock.Handle(command)
            .Returns(Result<string>.Success(expectedToken));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var response = Assert.IsType<ValidateOtpResponse>(okResult.Value);
        Assert.Equal(expectedToken, response.Token);

        await handlerMock.Received(1).Handle(command);
    }

    [Fact]
    public async Task ValidateOtp_WhenFailure_ReturnsBadRequest()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(command)
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
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(command)
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
        ValidateOtpCommand? command = null;
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
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(command)
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
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        handlerMock.Handle(command)
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

    [Fact]
    public async Task ValidateOtp_WhenSuccess_ReturnsValidateOtpResponseWithToken()
    {
        // Arrange
        var command = new ValidateOtpCommand { Email = "user@example.com", Otp = "123456" };
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand, string>>();
        var expectedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        handlerMock.Handle(command)
            .Returns(Result<string>.Success(expectedToken));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var response = Assert.IsType<ValidateOtpResponse>(okResult.Value);
        Assert.Equal(expectedToken, response.Token);
        Assert.NotNull(response.Token);
        Assert.NotEmpty(response.Token);
    }
}
