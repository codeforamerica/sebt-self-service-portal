using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth;

public class ValidateOtpCommandHandlerTests
{
    private readonly IOtpRepository otpRepository = Substitute.For<IOtpRepository>();
    private readonly IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly NullLogger<ValidateOtpCommandHandler> logger = NullLogger<ValidateOtpCommandHandler>.Instance;
    private readonly IValidator<ValidateOtpCommand> validator = new DataAnnotationsValidator<ValidateOtpCommand>(null!);

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenUnexpiredOtpAndEmailAreValid()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
            validator,
            logger);

        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        jwtTokenService.GenerateToken(command.Email).Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("test.jwt.token", successResult.Value);
        jwtTokenService.Received(1).GenerateToken(command.Email);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsExpired()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
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
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        Assert.Contains("Otp", failedResult.Errors.Select(e => e.Key));
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldRetrieveCachedOtpObjectFromRepository_WhenEmailIsFound()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };
        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode(command.Otp, command.Email));
        jwtTokenService.GenerateToken(command.Email).Returns("test.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await otpRepository.Received(1).GetOtpCodeByEmailAsync(command.Email);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpDoesNotMatchEmail()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
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
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsNotSixDigits()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
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
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<string>());
    }
    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenEmailFormatIsIncorrect()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
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
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpDoesNotExist()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
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
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        Assert.Contains("Otp", failedResult.Errors.Select(e => e.Key));
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsNull()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(command.Email)
            .Returns((OtpCode?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        Assert.Contains("Otp", failedResult.Errors.Select(e => e.Key));
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<string>());
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteOtpAfterSuccessfulValidation()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        jwtTokenService.GenerateToken(command.Email).Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await otpRepository.Received(1).DeleteOtpCodeByEmailAsync(command.Email);
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenJwtTokenGenerationThrowsException()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        jwtTokenService
            .When(x => x.GenerateToken(Arg.Any<string>()))
            .Do(x => throw new Exception("JWT generation failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<DependencyFailedResult<string>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failedResult.Reason);
        Assert.Contains("error occurred while generating", failedResult.Message, StringComparison.OrdinalIgnoreCase);
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldNotDeleteOtp_WhenJwtTokenGenerationFails()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        jwtTokenService
            .When(x => x.GenerateToken(Arg.Any<string>()))
            .Do(x => throw new Exception("JWT generation failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        // OTP should not be deleted if token generation fails
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }
}
