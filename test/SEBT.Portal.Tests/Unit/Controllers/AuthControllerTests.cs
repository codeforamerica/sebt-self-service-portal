using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class AuthControllerTests
{
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var logger = NullLogger<AuthController>.Instance;
        _controller = new AuthController(logger);
    }

    private void SetupAuthenticatedUser(string email, string claimType = ClaimTypes.Email)
    {
        var claims = new List<Claim> { new Claim(claimType, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public void GetAuthorizationStatus_WhenUserIsAuthenticated_ReturnsOkWithEmail()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email);

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.True(response.IsAuthorized);
        Assert.Equal(email, response.Email);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenEmailClaimIsMissing_UsesNameIdentifier()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, ClaimTypes.NameIdentifier);

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.True(response.IsAuthorized);
        Assert.Equal(email, response.Email);
    }

    [Fact]
    public async Task RefreshToken_WhenSuccess_ReturnsOkWithNewToken()
    {
        // Arrange
        var email = "user@example.com";
        var expectedToken = "refreshed.jwt.token";
        SetupAuthenticatedUser(email);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email))
            .Returns(Result<string>.Success(expectedToken));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateOtpResponse>(okResult.Value);
        Assert.Equal(expectedToken, response.Token);
        await handlerMock.Received(1).Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email));
    }

    [Fact]
    public async Task RefreshToken_WhenEmailCannotBeExtracted_ReturnsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>(); // No email claim
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.NotNull(result);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
        await handlerMock.DidNotReceive().Handle(Arg.Any<RefreshTokenCommand>());
    }

    [Fact]
    public async Task RefreshToken_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";
        SetupAuthenticatedUser(email);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email))
            .Returns(Result<string>.PreconditionFailed(
                PreconditionFailedReason.NotFound,
                "User not found."));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.NotNull(result);
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Contains("User not found", errorResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshToken_WhenValidationFails_ReturnsBadRequestWithErrors()
    {
        // Arrange
        var email = "invalid-email";
        SetupAuthenticatedUser(email);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        var validationErrors = new[]
        {
            new ValidationError("Email", "Invalid email format.")
        };
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email))
            .Returns(Result<string>.ValidationFailed(validationErrors));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Verify errors are included in response
        var errorProperty = badRequestResult.Value.GetType().GetProperty("Errors");
        Assert.NotNull(errorProperty);
    }

    [Fact]
    public async Task RefreshToken_WhenDependencyFails_ReturnsBadRequest()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email))
            .Returns(Result<string>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "An error occurred while refreshing the authentication token."));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.NotNull(result);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
        Assert.Contains("error occurred while refreshing", badRequestResult.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshToken_ExtractsEmailFromEmailClaim()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Any<RefreshTokenCommand>())
            .Returns(Result<string>.Success("token"));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email));
    }

    [Fact]
    public async Task RefreshToken_ExtractsEmailFromNameIdentifier_WhenEmailClaimMissing()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, ClaimTypes.NameIdentifier);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Any<RefreshTokenCommand>())
            .Returns(Result<string>.Success("token"));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email));
    }

    [Fact]
    public async Task RefreshToken_ExtractsEmailFromIdentityName_WhenOtherClaimsMissing()
    {
        // Arrange
        var email = "user@example.com";
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.Name, email));
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Any<RefreshTokenCommand>())
            .Returns(Result<string>.Success("token"));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RefreshTokenCommand>(c => c.Email == email));
    }
}

