using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class OidcControllerTests
{
    private const string CoStateKey = "co";
    private const string TestSessionId = "test-session-id";
    private readonly IConfiguration _config;
    private readonly IPreAuthSessionStore _sessionStore;
    private readonly ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResult> _handler;
    private readonly OidcController _controller;

    public OidcControllerTests()
    {
        _config = Substitute.For<IConfiguration>();
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        // default allowlist + session store for tests.
        var allowlist = new StateAllowlist([CoStateKey]);
        _sessionStore = Substitute.For<IPreAuthSessionStore>();
        _sessionStore.CreateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new PreAuthSession
            {
                Id = "test-session-id",
                State = callInfo.ArgAt<string>(1),
                CodeVerifier = callInfo.ArgAt<string>(2),
                StateCode = callInfo.ArgAt<string>(0),
                RedirectUri = callInfo.ArgAt<string>(3),
                IsStepUp = callInfo.ArgAt<bool>(4)
            });

        _handler = Substitute.For<ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResult>>();

        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        _controller = new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            allowlist,
            _sessionStore,
            env)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    /// <summary>
    /// Sets up the controller's HttpContext with an <c>oidc_session</c> cookie.
    /// The controller reads only the cookie; all session validation is in the handler.
    /// Call before any <c>CompleteLogin</c> test that needs the cookie present.
    /// </summary>
    private void SetupSessionCookie()
    {
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";
    }

    [Fact]
    public async Task GetConfig_WhenAuthorizationEndpointMissing_Returns503()
    {
        _config["Oidc:AuthorizationEndpoint"].Returns((string?)null);
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetConfig_WhenClientIdMissing_Returns503()
    {
        _config["Oidc:AuthorizationEndpoint"].Returns("https://auth.example.com/authorize");
        _config["Oidc:ClientId"].Returns((string?)null);
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    /// <summary>
    /// GetConfig serves the pinned authorization endpoint from appsettings,
    /// creates a pre-auth session, sets the oidc_session cookie, and returns
    /// state + codeChallenge server-generated values.
    /// </summary>
    [Fact]
    public async Task GetConfig_WhenConfigSet_ReturnsPinnedAuthorizationEndpointAndSessionState()
    {
        const string pinnedAuthUrl = "https://auth.example.com/authorize";
        _config["Oidc:AuthorizationEndpoint"].Returns(pinnedAuthUrl);
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value.GetType();
        Assert.Equal("client-id", valueType.GetProperty("clientId")?.GetValue(okResult.Value));
        Assert.Equal(pinnedAuthUrl, valueType.GetProperty("authorizationEndpoint")?.GetValue(okResult.Value));
        // server now returns state + codeChallenge (never code_verifier)
        Assert.NotNull(valueType.GetProperty("state")?.GetValue(okResult.Value));
        Assert.NotNull(valueType.GetProperty("codeChallenge")?.GetValue(okResult.Value));
        Assert.Equal("S256", valueType.GetProperty("codeChallengeMethod")?.GetValue(okResult.Value));
        // code_verifier must never be exposed to the client
        Assert.Null(valueType.GetProperty("codeVerifier")?.GetValue(okResult.Value));
        Assert.Null(valueType.GetProperty("code_verifier")?.GetValue(okResult.Value));
    }

    /// <summary>
    /// requests for unknown state codes never reach the config lookup.
    /// This blocks the route parameter from being used as a tenant escape when the
    /// instance only has one state loaded.
    /// </summary>
    [Fact]
    public async Task GetConfig_WhenStateCodeNotInAllowlist_Returns400()
    {
        var testEnv = Substitute.For<IWebHostEnvironment>();
        testEnv.EnvironmentName.Returns("Development");
        var controller = new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            new StateAllowlist(["co"]),
            Substitute.For<IPreAuthSessionStore>(),
            testEnv)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetConfig("nm");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Contains("unsupported stateCode", error.Error);
        // Authorization endpoint must not be read at all for a rejected state.
        _ = _config.DidNotReceive()["Oidc:AuthorizationEndpoint"];
    }

    /// <summary>allowlist is case-insensitive; CO uppercase should resolve to "co".</summary>
    [Fact]
    public async Task GetConfig_WhenStateCodeCaseInsensitiveMatch_PassesAllowlistCheck()
    {
        // No auth endpoint mock — we only care that we get past the allowlist into the
        // "no config" 503 path, which proves the check itself accepted the input.
        _config["Oidc:AuthorizationEndpoint"].Returns((string?)null);

        var result = await _controller.GetConfig("CO");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    /// <summary>
    /// CompleteLogin builds the command with body fields + session cookie, then delegates
    /// to the handler. When the handler returns ValidationFailed for missing stateCode,
    /// the controller maps it to 400.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerRejectsForMissingStateCode_Returns400()
    {
        var body = new CompleteLoginRequest(StateCode: null, "callback.jwt.here");

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.ValidationFailed("StateCode", "State code is required."));

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    /// <summary>
    /// CompleteLogin builds the command with body fields + session cookie, then delegates
    /// to the handler. When the handler returns ValidationFailed for missing callbackToken,
    /// the controller maps it to 400.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerRejectsForMissingCallbackToken_Returns400()
    {
        var body = new CompleteLoginRequest(CoStateKey, CallbackToken: null);

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.ValidationFailed("CallbackToken", "Callback token is required."));

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    /// <summary>
    /// When the handler returns Unauthorized (e.g., missing/invalid session), the controller
    /// maps it to 403.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsUnauthorized_Returns403()
    {
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.Unauthorized(
                "Pre-auth session invalid, expired, or already used."));

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerSucceeds_SetsAuthCookieAndReturnsEmptyBody()
    {
        SetupSessionCookie();

        const string portalJwt = "portal-jwt-returned-by-handler";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.Success(
                new CompleteOidcLoginResult(portalJwt, expiresAt, ReturnUrl: null)));

        var body = new CompleteLoginRequest(CoStateKey, "valid.callback.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Null(response.ReturnUrl);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={portalJwt}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerSucceedsWithReturnUrl_Returns200WithReturnUrl()
    {
        SetupSessionCookie();

        const string portalJwt = "portal-jwt-returned-by-handler";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.Success(
                new CompleteOidcLoginResult(portalJwt, expiresAt, ReturnUrl: "/profile/address?q=1")));

        var body = new CompleteLoginRequest(
            CoStateKey,
            "valid.callback.token",
            IsStepUp: true,
            ReturnUrl: "/profile/address?q=1");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Equal("/profile/address?q=1", response.ReturnUrl);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={portalJwt}", setCookie);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsValidationFailed_Returns400()
    {
        SetupSessionCookie();

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.ValidationFailed("callbackToken", "Callback token must contain an email or sub claim."));

        var body = new CompleteLoginRequest(CoStateKey, "invalid.callback.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    /// <summary>
    /// Step-up that fails because user doesn't exist should return 400 from the handler.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsValidationFailedForStepUp_Returns400()
    {
        SetupSessionCookie();

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.ValidationFailed(
                "stepUp", "Step-up requires an existing session. Please sign in again."));

        var body = new CompleteLoginRequest(CoStateKey, "valid.callback.token", IsStepUp: true);

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    /// <summary>
    /// Verifies that the controller passes the correct fields through to the handler command:
    /// StateCode and CallbackToken from the body, SessionId from the cookie, ReturnUrl from the body.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenValid_PassesCorrectCommandToHandler()
    {
        SetupSessionCookie();

        const string portalJwt = "portal-jwt";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.Success(
                new CompleteOidcLoginResult(portalJwt, expiresAt, ReturnUrl: "/dashboard")));

        var body = new CompleteLoginRequest(
            CoStateKey,
            "the.callback.token",
            IsStepUp: true,
            ReturnUrl: "/dashboard");

        await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        await _handler.Received(1).Handle(
            Arg.Is<CompleteOidcLoginCommand>(c =>
                c.StateCode == CoStateKey &&
                c.CallbackToken == "the.callback.token" &&
                c.SessionId == TestSessionId &&
                c.ReturnUrl == "/dashboard"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that a handler returning DependencyFailed (e.g., signing key not configured)
    /// results in a BadRequest from the controller's failureMap.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsDependencyFailed_Returns502()
    {
        SetupSessionCookie();

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.DependencyFailed(
                Kernel.Results.DependencyFailedReason.ServiceUnavailable, "Signing key not configured."));

        var body = new CompleteLoginRequest(CoStateKey, "some.callback.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, objectResult.StatusCode);
    }
}
