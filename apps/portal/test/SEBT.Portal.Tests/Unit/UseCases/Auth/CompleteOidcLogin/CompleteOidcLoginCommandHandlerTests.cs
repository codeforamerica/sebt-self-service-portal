using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth.CompleteOidcLogin;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth.CompleteOidcLogin;

/// <summary>
/// Unit coverage for the OIDC complete-login orchestration: session enforcement,
/// callback-token verification, user resolution (existing-only for step-up,
/// get-or-create for login), and portal JWT minting. No AspNetCore host — cookies
/// and HTTP mapping are the controller's concern.
/// </summary>
public class CompleteOidcLoginCommandHandlerTests
{
    private const string TestSessionId = "test-session-id";
    private const string TestCallbackToken = "some.callback.token";
    private const string TestSub = "test-sub-12345";

    private readonly IPreAuthSessionStore _sessionStore = Substitute.For<IPreAuthSessionStore>();
    private readonly IOidcExchangeService _exchangeService = Substitute.For<IOidcExchangeService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IOidcTokenService _oidcTokenService = Substitute.For<IOidcTokenService>();
    private readonly IOidcCallbackFailureLogger _callbackFailureLogger =
        Substitute.For<IOidcCallbackFailureLogger>();

    private CompleteOidcLoginCommandHandler CreateHandler() => new(
        _sessionStore,
        _exchangeService,
        _userRepository,
        _oidcTokenService,
        _callbackFailureLogger,
        NullLogger<CompleteOidcLoginCommandHandler>.Instance);

    private static CompleteOidcLoginCommand CreateCommand(string? sessionId = TestSessionId) => new()
    {
        CallbackToken = TestCallbackToken,
        SessionId = sessionId
    };

    /// <summary>
    /// Configures the session store with a callback-completed session and a passing
    /// login-phase advance. Call before any test that should get past session enforcement.
    /// </summary>
    private void SetupPreAuthSession(bool isStepUp = false, string? returnUrl = null)
    {
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>())
            .Returns(new PreAuthSession
            {
                Id = TestSessionId,
                State = "test-state",
                CodeVerifier = "test-verifier",
                StateCode = "co",
                RedirectUri = "http://localhost:3000/callback",
                IsStepUp = isStepUp,
                ReturnUrl = returnUrl,
                Phase = PreAuthSessionPhase.CallbackCompleted
            });
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    /// <summary>Configures the exchange service to validate the callback token into the given claims.</summary>
    private void SetupValidToken(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        _exchangeService.ValidateCallbackToken(TestCallbackToken)
            .Returns(new OidcCallbackTokenResult { Principal = new ClaimsPrincipal(identity) });
    }

    // ── Session enforcement ──

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenSessionIdMissing()
    {
        var result = await CreateHandler().Handle(CreateCommand(sessionId: null));

        var forbidden = Assert.IsType<ForbiddenResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal("Missing pre-auth session.", forbidden.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_session" && e.Phase == "complete-login" && e.HttpStatus == 403));
        _exchangeService.DidNotReceiveWithAnyArgs().ValidateCallbackToken(default!);
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenSessionExpiredOrNotFound()
    {
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>())
            .Returns((PreAuthSession?)null);

        var result = await CreateHandler().Handle(CreateCommand());

        var forbidden = Assert.IsType<ForbiddenResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal("Pre-auth session invalid, expired, or already used.", forbidden.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_session" && e.SessionId == TestSessionId));
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenAdvanceFails()
    {
        // A hash mismatch or a losing race on the phase transition is treated as replay.
        SetupPreAuthSession();
        _sessionStore.TryAdvanceToLoginCompletedAsync(
                TestSessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateHandler().Handle(CreateCommand());

        var forbidden = Assert.IsType<ForbiddenResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal("Pre-auth session invalid, expired, or already used.", forbidden.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "replay" && e.Phase == "complete-login" && e.HttpStatus == 403));
        _exchangeService.DidNotReceiveWithAnyArgs().ValidateCallbackToken(default!);
    }

    [Fact]
    public async Task Handle_AdvancesWithCallbackTokenHash_AndRemovesSession()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        _userRepository.GetOrCreateUserByExternalIdAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((new User(), false));
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        await CreateHandler().Handle(CreateCommand());

        await _sessionStore.Received(1).TryAdvanceToLoginCompletedAsync(
            TestSessionId,
            IPreAuthSessionStore.HashCallbackToken(TestCallbackToken),
            Arg.Any<CancellationToken>());
        await _sessionStore.Received(1).RemoveAsync(TestSessionId, Arg.Any<CancellationToken>());
    }

    // ── Callback-token verification ──

    [Fact]
    public async Task Handle_ReturnsNotConfigured_WhenSigningKeyMissing()
    {
        SetupPreAuthSession();
        _exchangeService.ValidateCallbackToken(TestCallbackToken)
            .Returns(new OidcCallbackTokenResult { NotConfigured = true });

        var result = await CreateHandler().Handle(CreateCommand());

        var dependencyFailed = Assert.IsType<DependencyFailedResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal(DependencyFailedReason.NotConfigured, dependencyFailed.Reason);
        Assert.Equal("Complete-login not configured.", dependencyFailed.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "complete_login_not_configured" && e.HttpStatus == 503));
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailure_WhenCallbackTokenInvalid()
    {
        SetupPreAuthSession();
        _exchangeService.ValidateCallbackToken(TestCallbackToken)
            .Returns(new OidcCallbackTokenResult { Error = "IDX10223: token expired" });

        var result = await CreateHandler().Handle(CreateCommand());

        var validationFailed = Assert.IsType<ValidationFailedResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal("Invalid or expired callback token.", validationFailed.Errors.Single().Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "invalid_callback_token" && e.ApiError == "IDX10223: token expired"));
        await _userRepository.DidNotReceiveWithAnyArgs()
            .GetOrCreateUserByExternalIdAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailure_WhenTokenHasNoEmailOrSub()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("given_name", "Pat"));

        var result = await CreateHandler().Handle(CreateCommand());

        var validationFailed = Assert.IsType<ValidationFailedResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal(
            "Callback token must contain an email or sub claim.",
            validationFailed.Errors.Single().Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_identity_claim"));
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailure_WhenTokenHasEmailButNoSub()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("email", "user@example.com"));

        var result = await CreateHandler().Handle(CreateCommand());

        var validationFailed = Assert.IsType<ValidationFailedResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal("Callback token must contain a sub claim.", validationFailed.Errors.Single().Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_sub_claim"));
    }

    // ── User resolution + token minting ──

    [Fact]
    public async Task Handle_ReturnsToken_WhenLoginSucceeds()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        var user = new User { Email = "user@example.com" };
        _userRepository.GetOrCreateUserByExternalIdAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        var result = await CreateHandler().Handle(CreateCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal("portal-jwt", result.Value.Token);
        Assert.Null(result.Value.ReturnUrl);
    }

    /// <summary>
    /// Uses the sub claim (ExternalProviderId) for user lookup and passes email as a
    /// migration hint to GetOrCreateUserByExternalIdAsync.
    /// </summary>
    [Fact]
    public async Task Handle_LooksUpUserByExternalProviderId()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", "oidc-sub-abc123"));
        var user = new User { Email = "user@example.com" };
        _userRepository.GetOrCreateUserByExternalIdAsync(
                "oidc-sub-abc123", "user@example.com", Arg.Any<CancellationToken>())
            .Returns((user, false));
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        var result = await CreateHandler().Handle(CreateCommand());

        Assert.True(result.IsSuccess);

        // Should have been called with the exact sub and email values
        await _userRepository.Received(1).GetOrCreateUserByExternalIdAsync(
            "oidc-sub-abc123", "user@example.com", Arg.Any<CancellationToken>());

        // Should NOT use the old email-based lookup
        await _userRepository.DidNotReceive().GetOrCreateUserAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().GetUserByEmailAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// IAL derivation is handled entirely by the token service via claims in the JWT —
    /// the handler must never persist IAL to the database.
    /// </summary>
    [Fact]
    public async Task Handle_DoesNotPersistIalToDb()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        var user = new User { Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserByExternalIdAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        await CreateHandler().Handle(CreateCommand());

        // UpdateUserAsync should NEVER be called — IAL is in claims, not DB
        await _userRepository.DidNotReceive().UpdateUserAsync(
            Arg.Any<User>(), Arg.Any<CancellationToken>());

        // The user object's IAL should remain untouched (IAL1, not IAL1plus)
        Assert.Equal(UserIalLevel.IAL1, user.IalLevel);
    }

    [Fact]
    public async Task Handle_CallsTokenServiceWithIsStepUpFalse_ForNormalLogin()
    {
        SetupPreAuthSession();
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        var user = new User { Email = "user@example.com" };
        _userRepository.GetOrCreateUserByExternalIdAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        await CreateHandler().Handle(CreateCommand());

        _oidcTokenService.Received(1).GenerateForOidcLogin(user, Arg.Any<ClaimsPrincipal>(), false);
        // The non-step-up path uses get-or-create, never the existing-only lookup.
        await _userRepository.DidNotReceive().GetUserByExternalIdAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Step-up flows ──

    /// <summary>
    /// Step-up must not create a user; the IdP identity must already match a portal
    /// account from primary sign-in.
    /// </summary>
    [Fact]
    public async Task Handle_ReturnsPreconditionFailure_WhenStepUpAndNoExistingUser()
    {
        SetupPreAuthSession(isStepUp: true);
        SetupValidToken(new Claim("email", "new-user@example.com"), new Claim("sub", TestSub));
        _userRepository.GetUserByExternalIdAsync(TestSub, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler().Handle(CreateCommand());

        var preconditionFailed = Assert.IsType<PreconditionFailedResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, preconditionFailed.Reason);
        Assert.Equal("Step-up requires an existing session. Please sign in again.", preconditionFailed.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "step_up_user_not_found" && e.IsStepUp == true));

        await _userRepository.DidNotReceive().GetOrCreateUserByExternalIdAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSessionReturnUrl_WhenStepUpSucceeds()
    {
        SetupPreAuthSession(isStepUp: true, returnUrl: "/profile/address?q=1");
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        var user = new User { Email = "user@example.com" };
        _userRepository.GetUserByExternalIdAsync(TestSub, Arg.Any<CancellationToken>())
            .Returns(user);
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        var result = await CreateHandler().Handle(CreateCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal("/profile/address?q=1", result.Value.ReturnUrl);
        _oidcTokenService.Received(1).GenerateForOidcLogin(user, Arg.Any<ClaimsPrincipal>(), true);
    }

    /// <summary>
    /// When the session has no returnUrl (e.g. unsafe URL was rejected at authorize time),
    /// the response's returnUrl is null.
    /// </summary>
    [Fact]
    public async Task Handle_ReturnsNullReturnUrl_WhenStepUpSessionHasNone()
    {
        SetupPreAuthSession(isStepUp: true, returnUrl: null);
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        _userRepository.GetUserByExternalIdAsync(TestSub, Arg.Any<CancellationToken>())
            .Returns(new User { Email = "user@example.com" });
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        var result = await CreateHandler().Handle(CreateCommand());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ReturnUrl);
    }

    /// <summary>
    /// Step-up rejects when the token service returns a failure result (e.g. no
    /// verification claims from the IdP).
    /// </summary>
    [Fact]
    public async Task Handle_ReturnsValidationFailure_WhenTokenServiceFails()
    {
        SetupPreAuthSession(isStepUp: true);
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        _userRepository.GetUserByExternalIdAsync(TestSub, Arg.Any<CancellationToken>())
            .Returns(new User());
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), true)
            .Returns(Result<string>.DependencyFailed(
                DependencyFailedReason.BadRequest,
                "Step-up verification failed: IdP returned no verification claims."));

        var result = await CreateHandler().Handle(CreateCommand());

        var validationFailed = Assert.IsType<ValidationFailedResult<CompleteOidcLoginResponse>>(result);
        Assert.Equal("Step-up verification failed. Please try again.", validationFailed.Errors.Single().Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "token_generation_failed"));
    }

    /// <summary>
    /// The step-up decision comes from the server-side session only — there is nothing a
    /// client could send to force the step-up path for a session created as normal login.
    /// </summary>
    [Fact]
    public async Task Handle_UsesSessionIsStepUp_ToChooseUserResolutionPath()
    {
        SetupPreAuthSession(isStepUp: false);
        SetupValidToken(new Claim("email", "user@example.com"), new Claim("sub", TestSub));
        _userRepository.GetOrCreateUserByExternalIdAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((new User { Email = "user@example.com" }, false));
        _oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
            .Returns(Result<string>.Success("portal-jwt"));

        var result = await CreateHandler().Handle(CreateCommand());

        Assert.True(result.IsSuccess);
        // The non-step-up path calls GetOrCreateUserByExternalIdAsync, NOT GetUserByExternalIdAsync
        await _userRepository.Received(1).GetOrCreateUserByExternalIdAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().GetUserByExternalIdAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
