using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth;

public class RefreshTokenCommandHandlerTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly NullLogger<RefreshTokenCommandHandler> logger = NullLogger<RefreshTokenCommandHandler>.Instance;
    private readonly IValidator<RefreshTokenCommand> validator = new DataAnnotationsValidator<RefreshTokenCommand>(null!);
    private readonly RefreshTokenCommandHandler handler;

    public RefreshTokenCommandHandlerTests()
    {
        handler = new RefreshTokenCommandHandler(
            userRepository,
            jwtTokenService,
            validator,
            logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenUserExists()
    {
        // Arrange

        var command = new RefreshTokenCommand
        {
            Email = "user@example.com"
        };

        var user = new User
        {
            Email = command.Email,
            IdProofingStatus = IdProofingStatus.Completed
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email))
            .Returns("refreshed.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("refreshed.jwt.token", successResult.Value);
        await userRepository.Received(1).GetUserByEmailAsync(command.Email, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u => u.Email == command.Email && u.IdProofingStatus == IdProofingStatus.Completed));
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = string.Empty
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        Assert.Contains("Email", failedResult.Errors.Select(e => e.Key));
        await userRepository.DidNotReceive().GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenEmailIsInvalid()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "invalid-email"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult<string>>(result);
        Assert.Contains("Email", failedResult.Errors.Select(e => e.Key));
        await userRepository.DidNotReceive().GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldReturnPreconditionFailed_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "nonexistent@example.com"
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<PreconditionFailedResult<string>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, failedResult.Reason);
        Assert.Contains("User not found", failedResult.Message, StringComparison.OrdinalIgnoreCase);
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldGenerateTokenWithUpdatedIdProofingStatus()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "user@example.com"
        };

        var user = new User
        {
            Email = command.Email,
            IdProofingStatus = IdProofingStatus.InProgress,
            IdProofingSessionId = "session-abc-123",
            IdProofingCompletedAt = null,
            IdProofingExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>())
            .Returns("new.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Email == command.Email &&
            u.IdProofingStatus == IdProofingStatus.InProgress &&
            u.IdProofingSessionId == "session-abc-123"));
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenRepositoryThrowsException()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "user@example.com"
        };

        userRepository.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<User?>(new Exception("Database connection failed")));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<DependencyFailedResult<string>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failedResult.Reason);
        Assert.Contains("error occurred while refreshing", failedResult.Message, StringComparison.OrdinalIgnoreCase);
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenJwtServiceThrowsException()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "user@example.com"
        };

        var user = new User
        {
            Email = command.Email,
            IdProofingStatus = IdProofingStatus.NotStarted
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService
            .When(x => x.GenerateToken(Arg.Any<User>()))
            .Do(x => throw new Exception("JWT generation failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<DependencyFailedResult<string>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failedResult.Reason);
        Assert.Contains("error occurred while refreshing", failedResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_ShouldRetrieveUserFromRepository()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "user@example.com"
        };

        var user = new User
        {
            Email = command.Email,
            IdProofingStatus = IdProofingStatus.Completed
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await userRepository.Received(1).GetUserByEmailAsync(command.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassUserWithAllIdProofingData_ToJwtService()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "user@example.com"
        };

        var completedAt = DateTime.UtcNow.AddDays(-5);
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var user = new User
        {
            Email = command.Email,
            IdProofingStatus = IdProofingStatus.Completed,
            IdProofingSessionId = "session-xyz",
            IdProofingCompletedAt = completedAt,
            IdProofingExpiresAt = expiresAt
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Email == command.Email &&
            u.IdProofingStatus == IdProofingStatus.Completed &&
            u.IdProofingSessionId == "session-xyz" &&
            u.IdProofingCompletedAt == completedAt &&
            u.IdProofingExpiresAt == expiresAt));
    }
}

