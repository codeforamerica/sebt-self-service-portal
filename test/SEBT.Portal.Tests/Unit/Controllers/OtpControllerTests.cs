using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers;
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
        var command = new ValidateOtpCommand();
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.Success());

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(command);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ValidateOtp_WhenSuccess_ReturnsExpectedResult()
    {
        // Arrange
        var command = new ValidateOtpCommand();
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.Success());

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ValidateOtp_WhenFailure_ReturnsExpectedResult()
    {
        // Arrange
        var command = new ValidateOtpCommand();
        var handlerMock = Substitute.For<ICommandHandler<ValidateOtpCommand>>();
        handlerMock.Handle(command)
            .Returns(Result.ValidationFailed("message", "Invalid OTP"));

        // Act
        var result = await _controller.ValidateOtp(command, handlerMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("The operation failed due to validation", badRequestResult?.Value?.ToString() ?? string.Empty);
    }
}
