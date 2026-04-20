using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth;

public class ValidateOtpCommandHandlerTests
{
    private readonly IOtpRepository otpRepository = Substitute.For<IOtpRepository>();
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly NullLogger<ValidateOtpCommandHandler> logger = NullLogger<ValidateOtpCommandHandler>.Instance;
    private readonly IValidator<ValidateOtpCommand> validator = new DataAnnotationsValidator<ValidateOtpCommand>(null!);

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenUnexpiredOtpAndEmailAreValid()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);

        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("test.jwt.token", successResult.Value);
        await userRepository.Received(1).GetOrCreateUserAsync(command.Email, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u => u.Email == command.Email));
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsExpired()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
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
        await userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldRetrieveCachedOtpObjectFromRepository_WhenEmailIsFound()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };
        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };
        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService.GenerateToken(Arg.Any<User>())
            .Returns("test.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await otpRepository.Received(1).GetOtpCodeByEmailAsync(command.Email);
        await userRepository.Received(1).GetOrCreateUserAsync(command.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpDoesNotMatchEmail()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
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
        await userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsNotSixDigits()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
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
        await userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }
    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenEmailFormatIsIncorrect()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
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
        await userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpDoesNotExist()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
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
        await userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenOtpIsNull()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
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
        await userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteOtpAfterSuccessfulValidation()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

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
            userRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService
            .When(x => x.GenerateToken(Arg.Any<User>()))
            .Do(x => throw new Exception("JWT generation failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<DependencyFailedResult<string>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failedResult.Reason);
        Assert.Contains("error occurred while processing", failedResult.Message, StringComparison.OrdinalIgnoreCase);
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldNotDeleteOtp_WhenJwtTokenGenerationFails()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService
            .When(x => x.GenerateToken(Arg.Any<User>()))
            .Do(x => throw new Exception("JWT generation failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        // OTP should not be deleted if token generation fails
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldGetOrCreateUser_WhenOtpIsValid()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);

        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1plus
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email && u.IalLevel == UserIalLevel.IAL1plus))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await userRepository.Received(1).GetOrCreateUserAsync(command.Email, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u => u.Email == command.Email && u.IalLevel == UserIalLevel.IAL1plus));
    }

    [Fact]
    public async Task Handle_ShouldPassUserWithIalLevel_ToJwtTokenService()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1,
            IdProofingSessionId = "session-123",
            IdProofingCompletedAt = null,
            IdProofingExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService.GenerateToken(Arg.Any<User>())
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Email == command.Email &&
            u.IalLevel == UserIalLevel.IAL1 &&
            u.IdProofingSessionId == "session-123"));
    }

    [Fact]
    public async Task Handle_ShouldLogNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<ValidateOtpCommandHandler>>();
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            mockLogger);
        var command = new ValidateOtpCommand
        {
            Email = "newuser@example.com",
            Otp = "123456"
        };

        var newUser = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((newUser, true));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("New user authenticated via OTP") && o.ToString()!.Contains(PiiMasker.MaskEmail(command.Email)!) && !o.ToString()!.Contains(command.Email)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ShouldLogReturningUser_WhenUserExists()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<ValidateOtpCommandHandler>>();
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            mockLogger);
        var command = new ValidateOtpCommand
        {
            Email = "existinguser@example.com",
            Otp = "123456"
        };

        var existingUser = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1plus
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((existingUser, false));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Returning user authenticated via OTP") && o.ToString()!.Contains(PiiMasker.MaskEmail(command.Email)!) && !o.ToString()!.Contains(command.Email)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ShouldLogIalLevel_ForNewUser()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<ValidateOtpCommandHandler>>();
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            mockLogger);
        var command = new ValidateOtpCommand
        {
            Email = "newuser@example.com",
            Otp = "123456"
        };

        var newUser = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((newUser, true));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("New user authenticated via OTP") &&
                               o.ToString()!.Contains(PiiMasker.MaskEmail(command.Email)!) &&
                               !o.ToString()!.Contains(command.Email) &&
                               o.ToString()!.Contains("IAL1")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ShouldLogIdProofingStatus_ForReturningUser()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<ValidateOtpCommandHandler>>();
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            mockLogger);
        var command = new ValidateOtpCommand
        {
            Email = "existinguser@example.com",
            Otp = "123456"
        };

        var existingUser = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((existingUser, false));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Returning user authenticated via OTP") &&
                               o.ToString()!.Contains(PiiMasker.MaskEmail(command.Email)!) &&
                               !o.ToString()!.Contains(command.Email) &&
                               o.ToString()!.Contains("IAL1")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ShouldLogGeneralSuccessMessage_AfterUserTypeLogging()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<ValidateOtpCommandHandler>>();
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            mockLogger);
        var command = new ValidateOtpCommand
        {
            Email = "user@example.com",
            Otp = "123456"
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Is<string>(email => email == command.Email))
            .Returns(new OtpCode(command.Otp, command.Email));
        userRepository.GetOrCreateUserAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((user, true));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("test.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("OTP validated successfully and JWT token generated") &&
                               o.ToString()!.Contains(PiiMasker.MaskEmail(command.Email)!) &&
                               !o.ToString()!.Contains(command.Email)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Tests that Handle skips OTP repository lookup and deletion when BypassOtp is true,
    /// but still creates the user and returns a JWT token.
    /// The controller is responsible for determining when to set BypassOtp; the handler just respects the flag.
    /// </summary>
    [Fact]
    public async Task Handle_WhenBypassOtpIsTrue_SkipsOtpValidation_ReturnsJwt()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);

        var user = new User
        {
            Email = OtpBypassSettings.Email,
            IalLevel = UserIalLevel.None
        };

        userRepository.GetOrCreateUserAsync(OtpBypassSettings.Email, Arg.Any<CancellationToken>())
            .Returns((user, true));
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == OtpBypassSettings.Email))
            .Returns("bypass.jwt.token");

        var command = new ValidateOtpCommand
        {
            Email = OtpBypassSettings.Email,
            Otp = OtpBypassSettings.OtpCode,
            BypassOtp = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("bypass.jwt.token", successResult.Value);
        await otpRepository.DidNotReceive().GetOtpCodeByEmailAsync(Arg.Any<string>());
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
        await userRepository.Received(1).GetOrCreateUserAsync(OtpBypassSettings.Email, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u => u.Email == OtpBypassSettings.Email));
    }

    /// <summary>
    /// Tests that Handle does not delete the OTP when BypassOtp is true, since OTP was never stored.
    /// </summary>
    [Fact]
    public async Task Handle_WhenBypassOtpIsTrue_DoesNotDeleteOtp()
    {
        // Arrange
        var handler = new ValidateOtpCommandHandler(
            otpRepository,
            userRepository,
            jwtTokenService,
            validator,
            logger);

        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        userRepository.GetOrCreateUserAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns((user, false));
        jwtTokenService.GenerateToken(Arg.Any<User>())
            .Returns("bypass.jwt.token");

        var command = new ValidateOtpCommand
        {
            Email = "user@example.com",
            Otp = "123456",
            BypassOtp = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await otpRepository.DidNotReceive().DeleteOtpCodeByEmailAsync(Arg.Any<string>());
    }
}
