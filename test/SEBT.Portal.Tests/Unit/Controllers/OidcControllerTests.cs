using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class OidcControllerTests
{
    private const string CoStateKey = "co";
    private const string TestSessionId = "test-session-id";
    private readonly IConfiguration _config;
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtService;
    private readonly IPreAuthSessionStore _sessionStore;
    private readonly OidcController _controller;

    public OidcControllerTests()
    {
        _config = Substitute.For<IConfiguration>();
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");
        _userRepository = Substitute.For<IUserRepository>();
        _jwtService = Substitute.For<IJwtTokenService>();
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

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings(),
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        _controller = new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            _userRepository,
            _jwtService,
            jwtSettings,
            allowlist,
            _sessionStore,
            env,
            translator)
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
    private void SetupPreAuthSession(bool isStepUp = false, string stateCode = CoStateKey, string? returnUrl = null)
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
                ReturnUrl = returnUrl,
                Phase = PreAuthSessionPhase.CallbackCompleted
            });
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
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
            _userRepository,
            _jwtService,
            jwtSettings,
            new StateAllowlist(["co"]),
            Substitute.For<IPreAuthSessionStore>(),
            testEnv,
            new OidcVerificationClaimTranslator(new OidcVerificationClaimSettings(), new IdProofingValiditySettings(), NullLogger<OidcVerificationClaimTranslator>.Instance))
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

    [Fact]
    public async Task CompleteLogin_WhenCallbackTokenMissing_Returns400()
    {
        var body = new CompleteLoginRequest(CoStateKey, CallbackToken: null);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CompleteLogin_WhenSigningKeyNotConfigured_Returns503()
    {
        SetupPreAuthSession();
        _config["Oidc:CompleteLoginSigningKey"].Returns((string?)null);
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task CompleteLogin_WhenCallbackTokenHasNoEmailOrSub_Returns400()
    {
        SetupPreAuthSession();
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateCallbackTokenWithClaims(signingKey, new Claim("given_name", "Pat"));
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Callback token must contain an email or sub claim.", errorResponse.Error);
    }

    [Fact]
    public async Task CompleteLogin_WhenValidCallbackToken_SetsAuthCookieAndReturnsEmptyBody()
    {
        SetupPreAuthSession();
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        const string portalJwt = "portal-jwt-returned-by-service";
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(portalJwt);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Null(response.ReturnUrl);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={portalJwt}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Step-up must not create a user; IdP email must already match a portal account from primary sign-in.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenStepUpAndNoExistingUser_Returns400()
    {
        SetupPreAuthSession(isStepUp: true);
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "new-user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken, IsStepUp: true);

        _userRepository.GetUserByEmailAsync("new-user@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var errorProp = badRequest.Value.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal(
            "Step-up requires an existing session. Please sign in again.",
            errorProp.GetValue(badRequest.Value) as string);

        await _userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteLogin_WhenStepUpAndSafeReturnUrl_Returns200WithReturnUrl()
    {
        SetupPreAuthSession(isStepUp: true, returnUrl: "/profile/address?q=1");
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        const string portalJwt = "portal-jwt-returned-by-service";
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(portalJwt);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Equal("/profile/address?q=1", response.ReturnUrl);

        var setCookie = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains($"{AuthCookies.AuthCookieName}={portalJwt}", setCookie);
    }

    /// <summary>
    /// When the session has no returnUrl (e.g. unsafe URL was rejected at authorize time),
    /// complete-login should return null returnUrl.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenStepUpWithNoSessionReturnUrl_OmitsReturnUrlFromResponse()
    {
        SetupPreAuthSession(isStepUp: true, returnUrl: null);
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt");

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CompleteLoginResponse>(okResult.Value);
        Assert.Null(response.ReturnUrl);
    }

    /// <summary>
    /// CompleteLogin must use the session's IsStepUp value, not the body's. A client
    /// should not be able to initiate a non-step-up flow and then send isStepUp=true
    /// on complete-login to trigger the IAL1+ upgrade path.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenBodyIsStepUpDiffersFromSession_UsesSessionValue()
    {
        // Set oidc_session cookie
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";

        // Session was created as non-step-up
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
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);
        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");

        // Body lies: says IsStepUp=true, but session says false
        var body = new CompleteLoginRequest(CoStateKey, callbackToken, IsStepUp: true);

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt");

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        // Should succeed (non-step-up path), NOT the step-up path
        Assert.IsType<OkObjectResult>(result);

        // The non-step-up path calls GetOrCreateUserAsync, NOT GetUserByEmailAsync
        await _userRepository.Received(1).GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #region OIDC verification claim reconciliation

    /// <summary>
    /// Helper that creates a controller with the verification claim translator wired up.
    /// </summary>
    private OidcController CreateControllerWithTranslator(
        OidcVerificationClaimSettings? claimSettings = null,
        IdProofingValiditySettings? validitySettings = null)
    {
        var translator = new OidcVerificationClaimTranslator(
            claimSettings ?? new OidcVerificationClaimSettings(),
            validitySettings ?? new IdProofingValiditySettings { ValidityDays = 1826 },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        return new OidcController(
            _config,
            NullLogger<OidcController>.Instance,
            _userRepository,
            _jwtService,
            jwtSettings,
            new StateAllowlist([CoStateKey]),
            _sessionStore,
            env,
            translator)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private void SetupPreAuthSessionForController(OidcController controller, bool isStepUp = false)
    {
        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{OidcSessionCookie.CookieName}={TestSessionId}";
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>())
            .Returns(new PreAuthSession
            {
                Id = TestSessionId,
                State = "test-state",
                CodeVerifier = "test-verifier",
                StateCode = CoStateKey,
                RedirectUri = "http://localhost:3000/callback",
                IsStepUp = isStepUp,
                Phase = PreAuthSessionPhase.CallbackCompleted
            });
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task CompleteLogin_WhenOidcClaimsContainFreshVerification_UpdatesUserToIAL1plus()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var controller = CreateControllerWithTranslator();
        SetupPreAuthSessionForController(controller);

        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var callbackToken = CreateCallbackTokenWithClaims(signingKey,
            new Claim("email", "user@example.com"),
            new Claim("socureIdVerificationLevel", "1.5"),
            new Claim("socureIdVerificationDate", verificationDate));
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt");

        await controller.CompleteLogin(body, CancellationToken.None);

        // User should have been updated to IAL1plus
        await _userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IalLevel == UserIalLevel.IAL1plus
                           && u.IdProofingStatus == IdProofingStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteLogin_WhenOidcVerificationExpired_ResetsUserToIAL1()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var controller = CreateControllerWithTranslator(
            validitySettings: new IdProofingValiditySettings { ValidityDays = 365 });
        SetupPreAuthSessionForController(controller);

        // Verification date is 2 years ago, but validity is only 1 year
        var verificationDate = DateTime.UtcNow.AddYears(-2).ToString("o");
        var callbackToken = CreateCallbackTokenWithClaims(signingKey,
            new Claim("email", "user@example.com"),
            new Claim("socureIdVerificationLevel", "1.5"),
            new Claim("socureIdVerificationDate", verificationDate));
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1plus };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt");

        await controller.CompleteLogin(body, CancellationToken.None);

        // User should have been reset to IAL1 with Expired status
        await _userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IalLevel == UserIalLevel.IAL1
                           && u.IdProofingStatus == IdProofingStatus.Expired),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteLogin_WhenNoVerificationClaims_DoesNotChangeIal()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var controller = CreateControllerWithTranslator();
        SetupPreAuthSessionForController(controller);

        // No socureIdVerificationLevel claim
        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt");

        await controller.CompleteLogin(body, CancellationToken.None);

        // No verification claims → no IAL reconciliation update
        // Only the initial IAL1 bump may have been called (user is already IAL1)
        await _userRepository.DidNotReceive().UpdateUserAsync(
            Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    #endregion

    /// <summary>Callback token issuer/audience must match <c>Oidc:CallbackRedirectUri</c> (trimmed).</summary>
    private const string TestCallbackTokenAudience = "http://localhost:3000/callback";

    private static string CreateValidCallbackToken(string signingKey, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new("email", email) };
        var token = new JwtSecurityToken(
            issuer: TestCallbackTokenAudience,
            audience: TestCallbackTokenAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateCallbackTokenWithClaims(string signingKey, params Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestCallbackTokenAudience,
            audience: TestCallbackTokenAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    #region Authorize endpoint

    /// <summary>
    /// Creates a mock <see cref="IOidcExchangeService"/> that returns a discovery config
    /// with the given authorization endpoint. Used by Authorize endpoint tests.
    /// </summary>
    private static IOidcExchangeService MockExchangeServiceWithDiscovery(string authorizationEndpoint)
    {
        var exchangeService = Substitute.For<IOidcExchangeService>();
        var discoveryConfig = new OpenIdConnectConfiguration
        {
            AuthorizationEndpoint = authorizationEndpoint
        };
        exchangeService.GetDiscoveryConfigAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(discoveryConfig);
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
        exchangeService.GetDiscoveryConfigAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<OpenIdConnectConfiguration>(_ => throw new InvalidOperationException("discovery endpoint not configured"));

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

    #endregion

}
