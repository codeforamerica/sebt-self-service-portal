using System.Linq;
using System.Security.Claims;
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
            Email = "user@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1plus
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Email == command.Email), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("refreshed.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("refreshed.jwt.token", successResult.Value);
        await userRepository.Received(1).GetUserByEmailAsync(command.Email, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u => u.Email == command.Email && u.IalLevel == UserIalLevel.IAL1plus), Arg.Any<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = string.Empty,
            CurrentPrincipal = new ClaimsPrincipal()
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
            Email = "invalid-email",
            CurrentPrincipal = new ClaimsPrincipal()
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
            Email = "nonexistent@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
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
            Email = "user@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1,
            IdProofingSessionId = "session-abc-123",
            IdProofingCompletedAt = null
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("new.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Email == command.Email &&
            u.IalLevel == UserIalLevel.IAL1 &&
            u.IdProofingSessionId == "session-abc-123"), Arg.Any<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenRepositoryThrowsException()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            Email = "user@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
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
            Email = "user@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.None
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService
            .When(x => x.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>()))
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
            Email = "user@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
        };

        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1plus
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
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
            Email = "user@example.com",
            CurrentPrincipal = new ClaimsPrincipal()
        };

        var completedAt = DateTime.UtcNow.AddDays(-5);
        var user = new User
        {
            Email = command.Email,
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingSessionId = "session-xyz",
            IdProofingCompletedAt = completedAt
        };

        userRepository.GetUserByEmailAsync(Arg.Is<string>(email => email == command.Email), Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Email == command.Email &&
            u.IalLevel == UserIalLevel.IAL1plus &&
            u.IdProofingSessionId == "session-xyz" &&
            u.IdProofingCompletedAt == completedAt), Arg.Any<IReadOnlyDictionary<string, string>>());
    }
}

