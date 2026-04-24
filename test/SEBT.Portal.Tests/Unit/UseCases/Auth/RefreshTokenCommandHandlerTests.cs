using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
    private readonly ISessionRefreshTokenService jwtTokenService = Substitute.For<ISessionRefreshTokenService>();
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

    /// <summary>Builds a ClaimsPrincipal carrying a sub claim (the portal's user ID) plus
    /// any additional claims the test needs to pass through to the refreshed token.</summary>
    private static ClaimsPrincipal PrincipalWithSub(string userIdSub, params Claim[] extraClaims)
    {
        var claims = new List<Claim> { new("sub", userIdSub) };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(userId.ToString())
        };

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus
        };

        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateForSessionRefresh(Arg.Is<User>(u => u.Id == userId), Arg.Any<ClaimsPrincipal>())
            .Returns("refreshed.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("refreshed.jwt.token", successResult.Value);
        await userRepository.Received(1).GetUserByIdAsync(userId, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateForSessionRefresh(
            Arg.Is<User>(u => u.Id == userId && u.IalLevel == UserIalLevel.IAL1plus),
            Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Handle_WhenPrincipalHasNoSubClaim_ReturnsPreconditionFailed()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity())
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<PreconditionFailedResult<string>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, failedResult.Reason);
        await userRepository.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Handle_WhenPrincipalSubIsNotAGuid_ReturnsPreconditionFailed()
    {
        // Arrange — sub is present but malformed (e.g. legacy email-based sub)
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub("not-a-guid")
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<PreconditionFailedResult<string>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, failedResult.Reason);
        await userRepository.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnPreconditionFailed_WhenUserDoesNotExist()
    {
        // Arrange
        var missingUserId = Guid.NewGuid();
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(missingUserId.ToString())
        };

        userRepository.GetUserByIdAsync(missingUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<PreconditionFailedResult<string>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, failedResult.Reason);
        Assert.Contains("User not found", failedResult.Message, StringComparison.OrdinalIgnoreCase);
        jwtTokenService.DidNotReceive().GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Handle_ShouldGenerateTokenWithUpdatedIdProofingStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(userId.ToString())
        };

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1,
            IdProofingSessionId = "session-abc-123",
            IdProofingCompletedAt = null
        };

        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>())
            .Returns("new.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateForSessionRefresh(Arg.Is<User>(u =>
            u.Id == userId &&
            u.IalLevel == UserIalLevel.IAL1 &&
            u.IdProofingSessionId == "session-abc-123"), Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenRepositoryThrowsException()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(Guid.NewGuid().ToString())
        };

        userRepository.GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<User?>(new Exception("Database connection failed")));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<DependencyFailedResult<string>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failedResult.Reason);
        Assert.Contains("error occurred while refreshing", failedResult.Message, StringComparison.OrdinalIgnoreCase);
        jwtTokenService.DidNotReceive().GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenJwtServiceThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(userId.ToString())
        };

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService
            .When(x => x.GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>()))
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
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(userId.ToString())
        };

        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus
        };

        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await userRepository.Received(1).GetUserByIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassUserWithAllIdProofingData_ToJwtService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub(userId.ToString())
        };

        var completedAt = DateTime.UtcNow.AddDays(-5);
        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingSessionId = "session-xyz",
            IdProofingCompletedAt = completedAt
        };

        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateForSessionRefresh(Arg.Is<User>(u =>
            u.Id == userId &&
            u.IalLevel == UserIalLevel.IAL1plus &&
            u.IdProofingSessionId == "session-xyz" &&
            u.IdProofingCompletedAt == completedAt), Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Handle_WhenOidcUser_PreservesIalFromExistingJwtClaims()
    {
        // Arrange — OIDC user whose IdP-sourced IAL lives in the existing JWT,
        // not in the DB (where IalLevel is None).
        var userId = Guid.NewGuid();
        var principal = PrincipalWithSub(
            userId.ToString(),
            new Claim("email", "user@example.com"),
            new Claim(JwtClaimTypes.Ial, "1plus"),
            new Claim(JwtClaimTypes.IdProofingStatus, "2")); // Completed

        var command = new RefreshTokenCommand { CurrentPrincipal = principal };

        var user = new User
        {
            Id = userId,
            ExternalProviderId = "pingone-sub-12345",
            IalLevel = UserIalLevel.None
        };

        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateForSessionRefresh(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>())
            .Returns("refreshed-jwt");

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);
        // IAL from JWT claims (1plus) was passed through, not DB (None).
        jwtTokenService.Received(1).GenerateForSessionRefresh(
            Arg.Any<User>(),
            Arg.Is<ClaimsPrincipal>(p =>
                p.FindFirstValue(JwtClaimTypes.Ial) == "1plus"));
    }
}
