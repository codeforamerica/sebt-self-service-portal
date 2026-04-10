using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
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
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });
        _controller = new AuthController(logger, jwtSettings);
    }

    private void SetupAuthenticatedUser(string email, string claimType = "email")
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
    public void GetAuthorizationStatus_WhenIalAndIdProofingClaimsPresent_IncludesThemInResponse()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("email", "user@example.com"),
            new Claim(JwtClaimTypes.Ial, "1plus"),
            new Claim(JwtClaimTypes.IdProofingStatus, "2"),
            new Claim(JwtClaimTypes.IdProofingCompletedAt, "1735689600"),
            new Claim(JwtClaimTypes.IdProofingExpiresAt, "1767225600")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.Equal("1plus", response.Ial);
        Assert.Equal(2, response.IdProofingStatus);
        Assert.Equal(1735689600L, response.IdProofingCompletedAt);
        Assert.Equal(1767225600L, response.IdProofingExpiresAt);
    }

    [Fact]
    public void Logout_ClearsAuthCookie()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = _controller.Logout();

        // Assert
        Assert.IsType<NoContentResult>(result);
        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}=", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenEmailClaimIsMissing_UsesSubClaim()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, "sub");

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
    public async Task RefreshToken_WhenSuccess_ReturnsNoContentAndSetsAuthCookie()
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
        Assert.IsType<NoContentResult>(result);
        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={expectedToken}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
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
    public async Task RefreshToken_ExtractsEmailFromSubClaim_WhenEmailClaimMissing()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, "sub");

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

    [Fact]
    public async Task RefreshToken_ExtractsEmailFromShortEmailClaim_WhenJwtHandlerMappedClaims()
    {
        // Arrange: JWT Bearer maps inbound claims to short names; principal may have "email" not ClaimTypes.Email
        var email = "user@example.com";
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("email", email));
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

    [Fact]
    public async Task RefreshToken_ExtractsEmailFromSubClaim_WhenJwtHandlerMappedClaims()
    {
        // Arrange: OIDC tokens often use "sub" as the user identifier; ensure we accept it for refresh
        var email = "user@example.com";
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("sub", email));
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

