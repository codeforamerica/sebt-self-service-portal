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
    /// Sets up the controller's HttpContext with an <c>oidc_session</c> cookie and
    /// configures the session store mock to accept <c>TryAdvanceToLoginCompletedAsync</c>.
    /// Call before any <c>CompleteLogin</c> test that should get past session enforcement.
    /// </summary>
    private void SetupPreAuthSession(bool isStepUp = false, string stateCode = CoStateKey)
    {
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>())
            .Returns(new PreAuthSession
            {
                Id = TestSessionId,
                State = "test-state",
                CodeVerifier = "test-verifier",
                StateCode = stateCode,
                RedirectUri = "http://localhost:3000/callback",
                IsStepUp = isStepUp,
                Phase = PreAuthSessionPhase.CallbackCompleted
            });
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
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

    [Fact]
    public async Task CompleteLogin_WhenStateCodeMissing_Returns400()
    {
        var body = new CompleteLoginRequest(StateCode: null, "callback.jwt.here");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CompleteLogin_WhenCallbackTokenMissing_Returns400()
    {
        var body = new CompleteLoginRequest(CoStateKey, CallbackToken: null);

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    /// <summary>
    /// complete-login must reject stateCodes outside the allowlist before
    /// parsing the callback token, closing the "unknown tenant" entry point.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenStateCodeNotInAllowlist_Returns400()
    {
        var body = new CompleteLoginRequest("nm", "callback.jwt.here");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Contains("unsupported stateCode", error.Error);
    }

    /// <summary>
    /// CompleteLogin must reject requests when the oidc_session cookie is missing.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenMissingSessionCookie_Returns403()
    {
        // No cookie set on the default HttpContext
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    /// <summary>
    /// CompleteLogin must reject requests where the body's stateCode does not match the
    /// session's stored stateCode, even if both are in the allowlist. This prevents a
    /// tenant-switching attack where a session created for one state is used with another.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenBodyStateCodeDiffersFromSession_Returns400()
    {
        // Session was created for "co", but body says "dc" — we need a second state in
        // the allowlist to test mismatch. Create a controller with both "co" and "dc".
        var multiStateAllowlist = new StateAllowlist(["co", "dc"]);
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        var sessionStore = Substitute.For<IPreAuthSessionStore>();
        var controller = new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            multiStateAllowlist,
            sessionStore,
            env)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Set oidc_session cookie
        controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";

        // Session was created for "co"
        sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>())
            .Returns(new PreAuthSession
            {
                Id = TestSessionId,
                State = "test-state",
                CodeVerifier = "test-verifier",
                StateCode = "co",
                RedirectUri = "http://localhost:3000/callback",
                IsStepUp = false,
                Phase = PreAuthSessionPhase.CallbackCompleted
            });
        sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Body says "dc" but session says "co" — should be rejected
        var body = new CompleteLoginRequest("dc", "some.callback.token");

        var result = await controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Contains("mismatch", error.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// CompleteLogin must reject replayed sessions (where the session phase cannot advance).
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenSessionReplay_Returns403()
    {
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>())
            .Returns(new PreAuthSession
            {
                Id = TestSessionId,
                State = "test-state",
                CodeVerifier = "test-verifier",
                StateCode = CoStateKey,
                RedirectUri = "http://localhost:3000/callback",
                IsStepUp = false,
                Phase = PreAuthSessionPhase.CallbackCompleted
            });
        // Phase advancement fails — session already used
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var body = new CompleteLoginRequest(CoStateKey, "some.callback.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerSucceeds_SetsAuthCookieAndReturnsEmptyBody()
    {
        SetupPreAuthSession();

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
        SetupPreAuthSession(isStepUp: true);

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
        SetupPreAuthSession();

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.ValidationFailed("callbackToken", "Callback token must contain an email or sub claim."));

        var body = new CompleteLoginRequest(CoStateKey, "invalid.callback.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    /// <summary>
    /// Step-up that fails because user doesn't exist should return 400 from the handler.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsValidationFailedForStepUp_Returns400()
    {
        SetupPreAuthSession(isStepUp: true);

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.ValidationFailed(
                "stepUp", "Step-up requires an existing session. Please sign in again."));

        var body = new CompleteLoginRequest(CoStateKey, "valid.callback.token", IsStepUp: true);

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    /// <summary>
    /// CompleteLogin must use the session's IsStepUp value, not the body's. The handler
    /// receives the command built from the session, so we verify the command passed to
    /// the handler has IsStepUp=false even when the body says true.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenBodyIsStepUpDiffersFromSession_UsesSessionValue()
    {
        // Session was created as non-step-up
        SetupPreAuthSession(isStepUp: false);

        const string portalJwt = "portal-jwt";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.Success(
                new CompleteOidcLoginResult(portalJwt, expiresAt, ReturnUrl: null)));

        // Body lies: says IsStepUp=true, but session says false
        var body = new CompleteLoginRequest(CoStateKey, "valid.callback.token", IsStepUp: true);

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        // Should succeed
        Assert.IsType<OkObjectResult>(result);

        // Verify the command sent to the handler used the session's IsStepUp (false), not the body's
        await _handler.Received(1).Handle(
            Arg.Is<CompleteOidcLoginCommand>(c => c.IsStepUp == false),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that the controller passes the callback token and return URL through
    /// to the handler command correctly.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenValid_PassesCorrectCommandToHandler()
    {
        SetupPreAuthSession(isStepUp: true);

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
                c.CallbackToken == "the.callback.token" &&
                c.IsStepUp == true &&
                c.ReturnUrl == "/dashboard"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that a handler returning DependencyFailed (e.g., signing key not configured)
    /// results in a BadRequest from the controller's failureMap.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsDependencyFailed_ReturnsBadRequest()
    {
        SetupPreAuthSession();

        _handler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CompleteOidcLoginResult>.DependencyFailed(
                Kernel.Results.DependencyFailedReason.ServiceUnavailable, "Signing key not configured."));

        var body = new CompleteLoginRequest(CoStateKey, "some.callback.token");

        var result = await _controller.CompleteLogin(body, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }
}
