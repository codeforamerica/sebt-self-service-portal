using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth.CompleteOidcLogin;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class OidcControllerTests
{
    private const string CoStateKey = "co";
    private const string TestSessionId = "test-session-id";
    private readonly IConfiguration _config;
    private readonly IPreAuthSessionStore _sessionStore;
    private readonly IOidcCallbackFailureLogger _callbackFailureLogger;
    private readonly ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResponse> _completeLoginHandler;
    private readonly OidcController _controller;

    public OidcControllerTests()
    {
        _config = Substitute.For<IConfiguration>();
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });

        // default allowlist + session store for tests.
        var allowlist = new StateAllowlist([CoStateKey]);
        _sessionStore = Substitute.For<IPreAuthSessionStore>();
        _sessionStore.CreateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => new PreAuthSession
            {
                Id = "test-session-id",
                State = callInfo.ArgAt<string>(1),
                CodeVerifier = callInfo.ArgAt<string>(2),
                StateCode = callInfo.ArgAt<string>(0),
                RedirectUri = callInfo.ArgAt<string>(3),
                IsStepUp = callInfo.ArgAt<bool>(4),
                ReturnUrl = callInfo.ArgAt<string?>(5)
            });

        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        _callbackFailureLogger = new OidcCallbackFailureLogger(
            NullLogger<OidcCallbackFailureLogger>.Instance,
            new HttpContextAccessor());

        _completeLoginHandler =
            Substitute.For<ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResponse>>();

        _controller = new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            _callbackFailureLogger,
            jwtSettings,
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

    /// <summary>Sets the <c>oidc_session</c> cookie on the controller's request.</summary>
    private void SetSessionCookie()
    {
        _controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";
    }

    [Fact]
    public async Task GetConfig_WhenClientIdMissing_Returns503()
    {
        _config["Oidc:ClientId"].Returns((string?)null);
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    /// <summary>
    /// GetConfig creates a pre-auth session, sets the oidc_session cookie, and returns
    /// state + codeChallenge server-generated values. The authorization endpoint is
    /// intentionally NOT included in the response.
    /// </summary>
    [Fact]
    public async Task GetConfig_WhenConfigSet_ReturnsSessionStateWithoutAuthorizationEndpoint()
    {
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value.GetType();
        Assert.Equal("client-id", valueType.GetProperty("clientId")?.GetValue(okResult.Value));
        // authorization endpoint must NOT be in the response (V04 fix)
        Assert.Null(valueType.GetProperty("authorizationEndpoint")?.GetValue(okResult.Value));
        // server returns state + codeChallenge (never code_verifier)
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
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });
        var testEnv = Substitute.For<IWebHostEnvironment>();
        testEnv.EnvironmentName.Returns("Development");
        var controller = new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            _callbackFailureLogger,
            jwtSettings,
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
    }

    /// <summary>allowlist is case-insensitive; CO uppercase should resolve to "co".</summary>
    [Fact]
    public async Task GetConfig_WhenStateCodeCaseInsensitiveMatch_PassesAllowlistCheck()
    {
        // No config mocked — we only care that we get past the allowlist into the
        // "no config" 503 path, which proves the check itself accepted the input.
        _config["Oidc:ClientId"].Returns((string?)null);

        var result = await _controller.GetConfig("CO");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    #region CompleteLogin HTTP mapping (orchestration is covered by CompleteOidcLoginCommandHandlerTests)

    private void SetupCompleteLoginResult(Result<CompleteOidcLoginResponse> result)
    {
        _completeLoginHandler.Handle(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    [Fact]
    public async Task CompleteLogin_WhenCallbackTokenMissing_Returns400WithoutDispatching()
    {
        var body = new CompleteLoginRequest(CoStateKey, CallbackToken: null);

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        await _completeLoginHandler.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task CompleteLogin_PassesCallbackTokenAndSessionCookieToHandler()
    {
        SetSessionCookie();
        SetupCompleteLoginResult(Result<CompleteOidcLoginResponse>.Success(
            new CompleteOidcLoginResponse("portal-jwt", ReturnUrl: null)));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        await _completeLoginHandler.Received(1).Handle(
            Arg.Is<CompleteOidcLoginCommand>(c =>
                c.CallbackToken == "some.jwt.token" && c.SessionId == TestSessionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerSucceeds_SetsAuthCookieAndClearsPreAuthCookie()
    {
        SetSessionCookie();
        const string portalJwt = "portal-jwt-returned-by-handler";
        SetupCompleteLoginResult(Result<CompleteOidcLoginResponse>.Success(
            new CompleteOidcLoginResponse(portalJwt, ReturnUrl: null)));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Null(response.ReturnUrl);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={portalJwt}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        // The consumed pre-auth session's cookie is cleared (expired) alongside.
        Assert.Contains(OidcSessionCookie.CookieName, setCookie);
    }

    [Fact]
    public async Task CompleteLogin_WhenStepUpReturnUrlPresent_ReturnsIt()
    {
        SetSessionCookie();
        SetupCompleteLoginResult(Result<CompleteOidcLoginResponse>.Success(
            new CompleteOidcLoginResponse("portal-jwt", ReturnUrl: "/profile/address?q=1")));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Equal("/profile/address?q=1", response.ReturnUrl);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerForbids_Returns403AndKeepsPreAuthCookie()
    {
        SetSessionCookie();
        SetupCompleteLoginResult(
            Result<CompleteOidcLoginResponse>.Forbidden("Missing pre-auth session."));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Missing pre-auth session.", error.Error);
        // Forbidden = the session was never consumed; the pre-auth cookie stays.
        Assert.DoesNotContain(
            OidcSessionCookie.CookieName,
            _controller.Response.Headers["Set-Cookie"].ToString());
    }

    [Fact]
    public async Task CompleteLogin_WhenNotConfigured_Returns503WithDevHint()
    {
        SetSessionCookie();
        SetupCompleteLoginResult(Result<CompleteOidcLoginResponse>.DependencyFailed(
            DependencyFailedReason.NotConfigured, "Complete-login not configured."));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
        var hint = statusResult.Value!.GetType().GetProperty("hint")?.GetValue(statusResult.Value) as string;
        Assert.Contains("Oidc:CompleteLoginSigningKey", hint);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsValidationFailure_Returns400WithItsMessage()
    {
        SetSessionCookie();
        SetupCompleteLoginResult(Result<CompleteOidcLoginResponse>.ValidationFailed(
            "callbackToken", "Invalid or expired callback token."));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Invalid or expired callback token.", error.Error);
    }

    [Fact]
    public async Task CompleteLogin_WhenHandlerReturnsPreconditionFailure_Returns400WithItsMessage()
    {
        SetSessionCookie();
        SetupCompleteLoginResult(Result<CompleteOidcLoginResponse>.PreconditionFailed(
            PreconditionFailedReason.NotFound,
            "Step-up requires an existing session. Please sign in again."));
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, _completeLoginHandler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Step-up requires an existing session. Please sign in again.", error.Error);
    }

    #endregion

    #region Authorize endpoint

    /// <summary>
    /// Creates a mock <see cref="IOidcExchangeService"/> that returns discovery info
    /// with the given authorization endpoint. Used by Authorize endpoint tests.
    /// </summary>
    private static IOidcExchangeService MockExchangeServiceWithDiscovery(string authorizationEndpoint)
    {
        var exchangeService = Substitute.For<IOidcExchangeService>();
        exchangeService.GetDiscoveryInfoAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new OidcDiscoveryInfo { AuthorizationEndpoint = authorizationEndpoint });
        return exchangeService;
    }

    /// <summary>
    /// Covers V05/T07a (stateCode tenant escape): invalid stateCode is rejected at the
    /// Authorize entry point. This replaces the removed integration tests
    /// V05_T07a_CompleteLogin_WithInvalidStateCode and V05_T07a_Callback_WithInvalidStateCode,
    /// and makes V06_T08aE (stateCode mismatch with valid session) impossible by design —
    /// Callback and CompleteLogin read stateCode from the session, not the body.
    /// </summary>
    [Fact]
    public async Task Authorize_WhenStateCodeNotInAllowlist_Returns400()
    {
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");

        var result = await _controller.Authorize("nm", exchangeService: exchangeService);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Contains("unsupported stateCode", error.Error);
    }

    [Fact]
    public async Task Authorize_WhenClientIdMissing_RedirectsToLogin()
    {
        _config["Oidc:ClientId"].Returns((string?)null);
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");

        var result = await _controller.Authorize(CoStateKey, exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenDiscoveryFails_RedirectsToLogin()
    {
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = Substitute.For<IOidcExchangeService>();
        exchangeService.GetDiscoveryInfoAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<OidcDiscoveryInfo>(_ => throw new InvalidOperationException("discovery endpoint not configured"));

        var result = await _controller.Authorize(CoStateKey, exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenDiscoveryMissingAuthorizationEndpoint_RedirectsToLogin()
    {
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery(authorizationEndpoint: "");

        var result = await _controller.Authorize(CoStateKey, exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenConfigured_Returns302WithCorrectUrlAndSetsCookie()
    {
        const string authEndpoint = "https://auth.pingone.com/env-id/as/authorize";
        _config["Oidc:ClientId"].Returns("test-client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        _config["Oidc:LanguageParam"].Returns("en");
        var exchangeService = MockExchangeServiceWithDiscovery(authEndpoint);

        var result = await _controller.Authorize(CoStateKey, exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(authEndpoint + "?", redirect.Url);
        Assert.Contains("response_type=code", redirect.Url);
        Assert.Contains("client_id=test-client-id", redirect.Url);
        Assert.Contains("redirect_uri=", redirect.Url);
        Assert.Contains("scope=openid", redirect.Url);
        Assert.Contains("state=", redirect.Url);
        Assert.Contains("code_challenge=", redirect.Url);
        Assert.Contains("code_challenge_method=S256", redirect.Url);
        Assert.Contains("prompt=login", redirect.Url);
        Assert.Contains("max_age=0", redirect.Url);
        Assert.Contains("language=en", redirect.Url);

        // Verify oidc_session cookie was set.
        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains(OidcSessionCookie.CookieName, setCookie);
    }

    /// <summary>
    /// Proves the session stores the validated stateCode from the Authorize endpoint.
    /// This is the mechanism that makes V06_T08aE (stateCode mismatch with valid session)
    /// impossible — Callback and CompleteLogin read stateCode from this session, not
    /// from the request body.
    /// </summary>
    [Fact]
    public async Task Authorize_WhenConfigured_CreatesPreAuthSessionWithCorrectValues()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");

        await _controller.Authorize(CoStateKey, stepUp: true, returnUrl: "/profile/address",
            exchangeService: exchangeService);

        await _sessionStore.Received(1).CreateAsync(
            CoStateKey,
            Arg.Any<string>(),
            Arg.Any<string>(),
            "http://localhost:3000/callback",
            true,
            "/profile/address",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WhenStepUpWithUnsafeReturnUrl_StoresNullReturnUrl()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");

        await _controller.Authorize(CoStateKey, stepUp: true, returnUrl: "https://evil.example/phish",
            exchangeService: exchangeService);

        await _sessionStore.Received(1).CreateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Is<string?>(s => s == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WhenNotStepUp_IgnoresReturnUrl()
    {
        _config["Oidc:ClientId"].Returns("test-client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");

        await _controller.Authorize(CoStateKey, stepUp: false, returnUrl: "/profile/address",
            exchangeService: exchangeService);

        await _sessionStore.Received(1).CreateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            false,
            Arg.Is<string?>(s => s == null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The authorization URL must never contain the code_verifier — only
    /// code_challenge is sent to the IdP.
    /// </summary>
    [Fact]
    public async Task Authorize_RedirectUrl_NeverContainsCodeVerifier()
    {
        _config["Oidc:ClientId"].Returns("test-client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");

        var result = await _controller.Authorize(CoStateKey, exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.DoesNotContain("code_verifier", redirect.Url);
    }

    /// <summary>
    /// DC-463: when the user already has IAL1+ with a fresh expiry, hitting Authorize with
    /// stepUp=true must short-circuit — no PingOne redirect, no new pre-auth session, and
    /// therefore no second Socure call. The user is redirected to the sanitized returnUrl.
    /// </summary>
    [Fact]
    public async Task Authorize_WhenStepUpAndUserAlreadyIal1Plus_ShortCircuitsToReturnUrl()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "1plus", idProofingExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: "/cards/replace",
            exchangeService: exchangeService);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/cards/replace", redirect.Url);

        await _sessionStore.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await exchangeService.DidNotReceive().GetDiscoveryInfoAsync(
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_WhenStepUpAndUserAlreadyIal2_ShortCircuits()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "2", idProofingExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: "/profile/address",
            exchangeService: exchangeService);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/profile/address", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenStepUpAndIal1Plus_WithNoReturnUrl_ShortCircuitsToDashboard()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "1plus", idProofingExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: null, exchangeService: exchangeService);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    /// <summary>
    /// An open-redirect returnUrl is sanitized to null. Short-circuit must still fall back
    /// to a safe relative path rather than echoing the unsafe value.
    /// </summary>
    [Fact]
    public async Task Authorize_WhenStepUpAndIal1Plus_WithUnsafeReturnUrl_ShortCircuitsToDashboard()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "1plus", idProofingExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: "https://evil.example/phish",
            exchangeService: exchangeService);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenStepUpAndIal1PlusButExpired_ProceedsToIdp()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "1plus", idProofingExpiresAt: DateTimeOffset.UtcNow.AddSeconds(-60));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: "/cards/replace",
            exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://auth.example.com/authorize?", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenStepUpAndIal1_ProceedsToIdp()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "1", idProofingExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: "/cards/replace",
            exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://auth.example.com/authorize?", redirect.Url);
    }

    [Fact]
    public async Task Authorize_WhenStepUpAndNoJwt_ProceedsToIdp()
    {
        _config["Oidc:StepUp:ClientId"].Returns("step-up-client-id");
        _config["Oidc:StepUp:RedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        // HttpContext.User is a default unauthenticated principal — no claims set.

        var result = await _controller.Authorize(
            CoStateKey, stepUp: true, returnUrl: "/cards/replace",
            exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://auth.example.com/authorize?", redirect.Url);
    }

    /// <summary>
    /// Normal (non-step-up) login must always proceed to the IdP even if the caller
    /// happens to have IAL1+ already. The guard is scoped to <c>stepUp=true</c> only.
    /// </summary>
    [Fact]
    public async Task Authorize_WhenNotStepUpAndUserAlreadyIal1Plus_StillProceedsToIdp()
    {
        _config["Oidc:ClientId"].Returns("test-client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        var exchangeService = MockExchangeServiceWithDiscovery("https://auth.example.com/authorize");
        SetUser(_controller, ial: "1plus", idProofingExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        var result = await _controller.Authorize(
            CoStateKey, stepUp: false, exchangeService: exchangeService);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("https://auth.example.com/authorize?", redirect.Url);
    }

    private static void SetUser(OidcController controller, string ial, DateTimeOffset idProofingExpiresAt)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.Ial, ial),
                new Claim(
                    JwtClaimTypes.IdProofingExpiresAt,
                    idProofingExpiresAt.ToUnixTimeSeconds().ToString())
            ],
            authenticationType: "Test");
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task ReportCallbackFailure_WithIdpRedirect_Returns204()
    {
        var result = await _controller.ReportCallbackFailure(
            new OidcCallbackFailureReportRequest(
                Reason: "idp_redirect",
                IdpError: "access_denied",
                IdpErrorDescription: "User cancelled"),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ReportCallbackFailure_WithInvalidReason_Returns400()
    {
        var result = await _controller.ReportCallbackFailure(
            new OidcCallbackFailureReportRequest(Reason: "not_a_real_reason"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    #endregion

}
