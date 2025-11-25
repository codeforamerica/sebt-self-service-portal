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
    private readonly IValidator<RequestOtpCommand> validator = new DataAnnotationsValidator<RequestOtpCommand>(null!);
    private readonly RequestOtpCommandHandler handler;
    public RequestOtpCommandHandlerTests()
    {
        // Arrange
        handler = new RequestOtpCommandHandler(
            validator,
            otpGenerator.Object,
            emailSender.Object,
            otpRepository.Object,
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

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// Tests that Handle generates an OTP when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldGenerateOtp_WhenEmailIsValid()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        otpGenerator.Verify(g => g.GenerateOtp(), Times.Once);
    }

    /// <summary>
    /// Tests that Handle sends an OTP email when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldSendOtpEmail_WhenEmailIsValid()
    {

        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        emailSender
            .Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Tests that Handle persists the OTP when a valid email is provided.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldPersistOtp_WhenEmailIsValid()
    {

        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        otpRepository
            .Verify(s => s.SaveOtpCodeAsync(It.IsAny<OtpCode>()), Times.Once);

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