using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;
using NSubstitute;
using SEBT.Portal.Core.Repositories;
using Sebt.Portal.Core.Models.Auth;
using NSubstitute.ReceivedExtensions;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth;

public class RequestOtpCommandHandlerTests
{

    private readonly IOtpGeneratorService otpGenerator = Substitute.For<IOtpGeneratorService>();
    private readonly IOtpSenderService emailSender = Substitute.For<IOtpSenderService>();
    private readonly IOtpRepository otpRepository = Substitute.For<IOtpRepository>();
    private readonly ILogger<RequestOtpCommandHandler> logger = Substitute.For<ILogger<RequestOtpCommandHandler>>();
    private readonly IValidator<RequestOtpCommand> validator = new DataAnnotationsValidator<RequestOtpCommand>(null!);
    private readonly RequestOtpCommandHandler handler;
    public RequestOtpCommandHandlerTests()
    {
        // Arrange
        handler = new RequestOtpCommandHandler(
            validator,
            otpGenerator,
            emailSender,
            otpRepository,
            logger);
    }
    /// <summary>
    /// Tests that Handle returns a Success Result when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenEmailIsValid()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.Success());
        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// Tests that Handle logs when an OTP is requested.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldLogInformation_WhenOtpIsRequested()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.Success());

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("OTP requested for email")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Tests that Handle logs a warning when validation fails.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldLogWarning_WhenValidationFails()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@" };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("OTP request failed for email")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Tests that Handle logs success when OTP request completes successfully.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldLogInformation_WhenOtpRequestSucceeds()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.Success());

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("OTP request successful for email")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Tests that Handle logs a warning when email sending fails.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldLogWarning_WhenEmailSendingFails()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.DependencyFailed(DependencyFailedReason.ConnectionFailed, "Email service unavailable"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("OTP request failed to send email")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Tests that Handle generates an OTP when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldGenerateOtp_WhenEmailIsValid()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.Success());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        otpGenerator.Received(1).GenerateOtp();
    }

    /// <summary>
    /// Tests that Handle sends an OTP email when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldSendOtpEmail_WhenEmailIsValid()
    {

        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.Success());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await emailSender
            .Received(1).SendOtpAsync(command.Email, Arg.Any<string>());
    }

    /// <summary>
    /// Tests that Handle persists the OTP when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldPersistOtp_WhenEmailIsValid()
    {

        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        emailSender.SendOtpAsync(command.Email, Arg.Any<string>())
            .Returns(Result.Success());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await otpRepository
            .Received(1).SaveOtpCodeAsync(Arg.Is<OtpCode>(otp => otp.Email == command.Email));

    }

    /// <summary>
    /// Tests that Handle returns a ValidationFailed Result when an invalid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenEmailIsInvalid()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
        Assert.Contains("Invalid email format.", failedResult.Errors.Select(e => e.Message));
    }
}
