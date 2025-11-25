using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.UseCases.Tests.Unit;

public class ValidateOtpCommandHandlerTests
{
    private readonly IOtpRepository otpRepository = Substitute.For<IOtpRepository>();
    private readonly NullLogger<ValidateOtpCommandHandler> logger = NullLogger<ValidateOtpCommandHandler>.Instance;
    private readonly IValidator<ValidateOtpCommand> validator = new DataAnnotationsValidator<ValidateOtpCommand>(null!);
    
    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenUnexpiredOtpAndEmailAreValid()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
            
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode(command.Otp, command.Email));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }
    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsExpired()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode(command.Otp, command.Email)
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
        Assert.Contains("Otp", failedResult.Errors.Select(e => e.Key));
    }

    [Fact]
    public async Task Handle_ValidateOtpValidity_WhenEmailIsFoundInRepository()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
               .Returns(new OtpCode(command.Otp, command.Email));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }
    [Fact]
    public async Task Handle_ShouldRetrieveCachedOtpObjectFromRepoistory_WhenEmailIsFound()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        // Assert
        await otpRepository.Received(1).GetOtpCodeByEmailAsync(command.Email);

    }

    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenOtpDoesNotMatchEmail()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode("", ""));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenOtpIsNotSixDigits()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "12345"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode("", ""));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
    }
    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenEmailFormatIsIncorrect()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode("", ""));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenOtpDoesNotExist()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

       otpRepository.GetOtpCodeByEmailAsync(command.Email)
            .Returns(new OtpCode("000000", command.Email));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
        Assert.Contains("Otp", failedResult.Errors.Select(e => e.Key));
    }
}
