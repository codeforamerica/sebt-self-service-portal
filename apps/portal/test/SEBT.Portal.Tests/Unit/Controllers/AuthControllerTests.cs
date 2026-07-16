using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Utilities;
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

    /// <summary>
    /// Sets up the authenticated user with a numeric sub claim (the portal JWT format)
    /// plus an optional email claim for GetAuthorizationStatus tests.
    /// </summary>
    private void SetupAuthenticatedUserWithSub(Guid userId, string? email = null)
    {
        var claims = new List<Claim> { new Claim("sub", userId.ToString()) };
        if (email != null)
        {
            claims.Add(new Claim("email", email));
        }
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
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
    public void GetAuthorizationStatus_WhenSubClaimPresent_PopulatesUserIdFromGuidClaim()
    {
        // Lock in the JWT sub claim → AuthorizationStatusResponse.UserId mapping
        // so analytics correlation never silently breaks if the claim shape shifts.
        var userId = Guid.NewGuid();
        SetupAuthenticatedUserWithSub(userId, email: "user@example.com");

        var result = _controller.GetAuthorizationStatus();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.Equal(userId, response.UserId);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenSubClaimAbsent_LeavesUserIdNull()
    {
        // No sub claim, only email. Response.UserId must be null rather than
        // some default Guid.Empty so the frontend can detect "no portal_id".
        SetupAuthenticatedUser("user@example.com");

        var result = _controller.GetAuthorizationStatus();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.Null(response.UserId);
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
            new Claim(JwtClaimTypes.IdProofingExpiresAt, "1767225600"),
            new Claim(JwtClaimTypes.IsCoLoaded, "true")
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
        Assert.True(response.IsCoLoaded);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenIsCoLoadedClaimFalse_ReturnsFalse()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("email", "user@example.com"),
            new Claim(JwtClaimTypes.IsCoLoaded, "false")
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
        Assert.False(response.IsCoLoaded);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenIsCoLoadedClaimAbsent_ReturnsNull()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email);

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.Null(response.IsCoLoaded);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenExpClaimPresent_IncludesExpiresAtInResponse()
    {
        // Arrange — the SPA needs to know when the session cookie expires so it can
        // schedule activity-gated refreshes (preventing indefinite-session bypass of
        // idle timeout). The JWT 'exp' claim is the source of truth.
        const long expEpochSeconds = 1767225600L;
        var claims = new List<Claim>
        {
            new Claim("email", "user@example.com"),
            new Claim("exp", expEpochSeconds.ToString())
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
        Assert.Equal(expEpochSeconds, response.ExpiresAt);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenExpClaimAbsent_ReturnsNullExpiresAt()
    {
        // Arrange
        SetupAuthenticatedUser("user@example.com");

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.Null(response.ExpiresAt);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenAuthTimePresent_IncludesAbsoluteExpiresAtInResponse()
    {
        // Arrange — controller derives AbsoluteExpiresAt from auth_time + AbsoluteExpirationMinutes.
        // Test controller is configured with AbsoluteExpirationMinutes=60.
        const long authTimeEpochSeconds = 1700000000L;
        var claims = new List<Claim>
        {
            new Claim("email", "user@example.com"),
            new Claim("auth_time", authTimeEpochSeconds.ToString())
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
        Assert.Equal(authTimeEpochSeconds + 60 * 60L, response.AbsoluteExpiresAt);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenAuthTimeAbsent_ReturnsNullAbsoluteExpiresAt()
    {
        // Arrange
        SetupAuthenticatedUser("user@example.com");

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.Null(response.AbsoluteExpiresAt);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenExpClaimNotANumber_ReturnsNullExpiresAt()
    {
        // Arrange — defensive: a non-numeric exp claim should not crash the endpoint
        var claims = new List<Claim>
        {
            new Claim("email", "user@example.com"),
            new Claim("exp", "not-a-number")
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
        Assert.Null(response.ExpiresAt);
    }

    [Fact]
    public async Task Logout_WithOidcConfigured_ClearsCookieAndRedirectsToIdpEndSessionEndpoint()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:DiscoveryEndpoint"] = "https://auth.pingone.com/.well-known/openid-configuration",
                ["Oidc:ClientId"] = "test-client-id",
                ["Oidc:CallbackRedirectUri"] = "https://portal.co.gov/callback"
            })
            .Build();

        var oidcExchangeService = Substitute.For<IOidcExchangeService>();
        var oidcConfig = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
        {
            EndSessionEndpoint = "https://auth.pingone.com/logout"
        };
        oidcExchangeService.GetDiscoveryConfigAsync(false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(oidcConfig));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.Logout(config, oidcExchangeService);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("https://auth.pingone.com/logout", redirectResult.Url);
        Assert.Contains("client_id=test-client-id", redirectResult.Url);
        Assert.Contains("post_logout_redirect_uri=", redirectResult.Url);
        Assert.Contains("%2Flogin", redirectResult.Url); // /login URL-encoded

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}=", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_WithoutOidcConfigured_ClearsCookieAndRedirectsToLogin()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var oidcExchangeService = Substitute.For<IOidcExchangeService>();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.Logout(config, oidcExchangeService);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login", redirectResult.Url);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}=", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_WhenDiscoveryFails_ClearsCookieAndRedirectsToLogin()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:DiscoveryEndpoint"] = "https://auth.pingone.com/.well-known/openid-configuration",
                ["Oidc:ClientId"] = "test-client-id",
                ["Oidc:CallbackRedirectUri"] = "https://portal.co.gov/callback"
            })
            .Build();

        var oidcExchangeService = Substitute.For<IOidcExchangeService>();
        oidcExchangeService.GetDiscoveryConfigAsync(false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Discovery failed"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.Logout(config, oidcExchangeService);

        // Assert — graceful fallback, don't strand the user
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login", redirectResult.Url);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}=", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAuthorizationStatus_WhenEmailClaimIsMissing_ReturnsNullEmail()
    {
        // Arrange: portal JWT has sub (user ID) but no email claim — OIDC users without stored email
        SetupAuthenticatedUserWithSub(userId: Guid.CreateVersion7());

        // Act
        var result = _controller.GetAuthorizationStatus();

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthorizationStatusResponse>(okResult.Value);
        Assert.True(response.IsAuthorized);
        Assert.Null(response.Email);
    }

    [Fact]
    public async Task RefreshToken_WhenSuccess_ReturnsNoContentAndSetsAuthCookie()
    {
        // Arrange — controller reads UserId from the sub claim (portal JWT format: sub = user.Id)
        var userId = Guid.NewGuid();
        const string expectedToken = "refreshed.jwt.token";
        SetupAuthenticatedUserWithSub(userId, email: "user@example.com");

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.CurrentPrincipal.GetUserId() == userId))
            .Returns(Result<string>.Success(expectedToken));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={expectedToken}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        await handlerMock.Received(1).Handle(Arg.Is<RefreshTokenCommand>(c => c.CurrentPrincipal.GetUserId() == userId));
    }

    [Fact]
    public async Task RefreshToken_WhenSubClaimMissing_ReturnsUnauthorized()
    {
        // Arrange — no sub claim means the controller cannot identify the user
        var claims = new List<Claim>(); // No sub claim
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
        var userId = Guid.NewGuid();
        SetupAuthenticatedUserWithSub(userId);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.CurrentPrincipal.GetUserId() == userId))
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
        // Arrange — handler returns a validation failure (e.g. some business rule violation)
        var userId = Guid.NewGuid();
        SetupAuthenticatedUserWithSub(userId);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        var validationErrors = new[]
        {
            new ValidationError("UserId", "User ID must be a positive integer.")
        };
        handlerMock.Handle(Arg.Any<RefreshTokenCommand>())
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
        var userId = Guid.NewGuid();
        SetupAuthenticatedUserWithSub(userId);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Is<RefreshTokenCommand>(c => c.CurrentPrincipal.GetUserId() == userId))
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
    public async Task RefreshToken_ExtractsUserIdFromSubClaim()
    {
        // Arrange — portal JWT has sub = user.Id (Guid string)
        var userId = Guid.NewGuid();
        SetupAuthenticatedUserWithSub(userId);

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();
        handlerMock.Handle(Arg.Any<RefreshTokenCommand>())
            .Returns(Result<string>.Success("token"));

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        await handlerMock.Received(1).Handle(Arg.Is<RefreshTokenCommand>(c => c.CurrentPrincipal.GetUserId() == userId));
    }

    [Fact]
    public async Task RefreshToken_WhenSubClaimIsNotAnInteger_ReturnsUnauthorized()
    {
        // Arrange — OIDC-style sub (non-integer) is rejected; portal JWT sub is always user.Id
        var claims = new List<Claim> { new Claim("sub", "not-an-integer") };
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var handlerMock = Substitute.For<ICommandHandler<RefreshTokenCommand, string>>();

        // Act
        var result = await _controller.RefreshToken(handlerMock);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
        await handlerMock.DidNotReceive().Handle(Arg.Any<RefreshTokenCommand>());
    }
}
