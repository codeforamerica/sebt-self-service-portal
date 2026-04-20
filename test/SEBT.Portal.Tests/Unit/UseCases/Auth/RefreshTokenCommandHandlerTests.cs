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
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub("1")
        };

        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus
        };

        userRepository.GetUserByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Id == 1), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("refreshed.jwt.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<string>>(result);
        Assert.Equal("refreshed.jwt.token", successResult.Value);
        await userRepository.Received(1).GetUserByIdAsync(1, Arg.Any<CancellationToken>());
        jwtTokenService.Received(1).GenerateToken(
            Arg.Is<User>(u => u.Id == 1 && u.IalLevel == UserIalLevel.IAL1plus),
            Arg.Any<IReadOnlyDictionary<string, string>>());
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
        await userRepository.DidNotReceive().GetUserByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_WhenPrincipalSubIsNotAnInteger_ReturnsPreconditionFailed()
    {
        // Arrange — sub is present but malformed (e.g. legacy email-based sub)
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub("not-a-number")
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<PreconditionFailedResult<string>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, failedResult.Reason);
        await userRepository.DidNotReceive().GetUserByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnPreconditionFailed_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub("999")
        };

        userRepository.GetUserByIdAsync(999, Arg.Any<CancellationToken>())
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
            CurrentPrincipal = PrincipalWithSub("1")
        };

        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1,
            IdProofingSessionId = "session-abc-123",
            IdProofingCompletedAt = null
        };

        userRepository.GetUserByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("new.token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Id == 1 &&
            u.IalLevel == UserIalLevel.IAL1 &&
            u.IdProofingSessionId == "session-abc-123"), Arg.Any<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenRepositoryThrowsException()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub("1")
        };

        userRepository.GetUserByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
            CurrentPrincipal = PrincipalWithSub("1")
        };

        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        userRepository.GetUserByIdAsync(1, Arg.Any<CancellationToken>())
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
            CurrentPrincipal = PrincipalWithSub("1")
        };

        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus
        };

        userRepository.GetUserByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await userRepository.Received(1).GetUserByIdAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassUserWithAllIdProofingData_ToJwtService()
    {
        // Arrange
        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = PrincipalWithSub("1")
        };

        var completedAt = DateTime.UtcNow.AddDays(-5);
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingSessionId = "session-xyz",
            IdProofingCompletedAt = completedAt
        };

        userRepository.GetUserByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("token");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        jwtTokenService.Received(1).GenerateToken(Arg.Is<User>(u =>
            u.Id == 1 &&
            u.IalLevel == UserIalLevel.IAL1plus &&
            u.IdProofingSessionId == "session-xyz" &&
            u.IdProofingCompletedAt == completedAt), Arg.Any<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public async Task Handle_WhenOidcUser_PreservesIalFromExistingJwtClaims()
    {
        // Arrange — OIDC user whose IdP-sourced IAL lives in the existing JWT,
        // not in the DB (where IalLevel is None).
        var principal = PrincipalWithSub(
            "1",
            new Claim("email", "user@example.com"),
            new Claim(JwtClaimTypes.Ial, "1plus"),
            new Claim(JwtClaimTypes.IdProofingStatus, "2")); // Completed

        var command = new RefreshTokenCommand { CurrentPrincipal = principal };

        var user = new User
        {
            Id = 1,
            ExternalProviderId = "pingone-sub-12345",
            IalLevel = UserIalLevel.None
        };

        userRepository.GetUserByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(user);
        jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("refreshed-jwt");

        // Act
        var result = await handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);
        // IAL from JWT claims (1plus) was passed through, not DB (None).
        jwtTokenService.Received(1).GenerateToken(
            Arg.Any<User>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(c =>
                c.ContainsKey(JwtClaimTypes.Ial) && c[JwtClaimTypes.Ial] == "1plus"));
    }
}
