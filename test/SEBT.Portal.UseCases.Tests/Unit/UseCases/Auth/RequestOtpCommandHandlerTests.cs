using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;
using Moq;
using SEBT.Portal.Core.Repositories;
using Sebt.Portal.Core.Models.Auth;

public class RequestOtpCommandHandlerTests
{

    private readonly Mock<IOtpGenerator> otpGenerator = new();
    private readonly Mock<IEmailSender> emailSender = new();
    private readonly Mock<IOtpRepository> otpRepository = new();
    private readonly NullLogger<RequestOtpCommandHandler> logger = NullLogger<RequestOtpCommandHandler>.Instance;

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenEmailIsValid()
    {

        // Arrange
        var handler = new RequestOtpCommandHandler(
           new DataAnnotationsValidator<RequestOtpCommand>(null!),
           otpGenerator.Object,
           emailSender.Object,
           otpRepository.Object,
           logger);
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldGenerateOtp_WhenEmailIsValid()
    {

        // Arrange
        var handler = new RequestOtpCommandHandler(
           new DataAnnotationsValidator<RequestOtpCommand>(null!),
           otpGenerator.Object,
           emailSender.Object,
           otpRepository.Object,
           logger);
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        otpGenerator.Verify(g => g.GenerateOtp(), Times.Once);
    }


    [Fact]
    public async Task Handle_ShouldSendOtpEmail_WhenEmailIsValid()
    {

        // Arrange
        var handler = new RequestOtpCommandHandler(
           new DataAnnotationsValidator<RequestOtpCommand>(null!),
           otpGenerator.Object,
           emailSender.Object,
           otpRepository.Object,
           logger);
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        emailSender
            .Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPersistOtp_WhenEmailIsValid()
    {

        // Arrange
        var handler = new RequestOtpCommandHandler(
           new DataAnnotationsValidator<RequestOtpCommand>(null!),
           otpGenerator.Object,
           emailSender.Object,
           otpRepository.Object,
           logger);
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        otpRepository
            .Verify(s => s.SaveOtpCodeAsync(It.IsAny<OtpCode>()), Times.Once);

    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenEmailIsInvalid()
    {
        // Arrange
        var handler = new RequestOtpCommandHandler(
            new DataAnnotationsValidator<RequestOtpCommand>(null!),
            otpGenerator.Object,
            emailSender.Object,
            otpRepository.Object,
            logger);
        var command = new RequestOtpCommand { Email = "user@" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
        Assert.Contains("Invalid email format.", failedResult.Errors.Select(e => e.Message));
    }
}