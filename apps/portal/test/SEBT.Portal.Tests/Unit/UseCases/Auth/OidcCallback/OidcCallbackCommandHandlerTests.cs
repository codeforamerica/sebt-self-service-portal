using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth.OidcCallback;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth.OidcCallback;

/// <summary>
/// Unit coverage for the OIDC callback orchestration: session enforcement, state (CSRF)
/// validation, replay protection, the code exchange, and session advancement. No
/// AspNetCore host — cookies and HTTP mapping are the controller's concern.
/// </summary>
public class OidcCallbackCommandHandlerTests
{
    private const string TestSessionId = "test-session-id";
    private const string TestState = "expected-state";
    private const string TestCodeVerifier = "test-code-verifier";
    private const string TestRedirectUri = "http://localhost:3000/callback";

    private readonly IPreAuthSessionStore _sessionStore = Substitute.For<IPreAuthSessionStore>();
    private readonly IOidcExchangeService _exchangeService = Substitute.For<IOidcExchangeService>();
    private readonly IOidcCallbackFailureLogger _callbackFailureLogger =
        Substitute.For<IOidcCallbackFailureLogger>();

    private OidcCallbackCommandHandler CreateHandler() => new(
        _sessionStore,
        _exchangeService,
        _callbackFailureLogger,
        NullLogger<OidcCallbackCommandHandler>.Instance);

    private static OidcCallbackCommand CreateCommand(
        string code = "auth-code",
        string? state = TestState,
        string? sessionId = TestSessionId) => new()
    {
        Code = code,
        State = state,
        SessionId = sessionId
    };

    private static PreAuthSession CreateSession(
        PreAuthSessionPhase phase = PreAuthSessionPhase.Created,
        bool isStepUp = false) =>
        new()
        {
            Id = TestSessionId,
            State = TestState,
            CodeVerifier = TestCodeVerifier,
            StateCode = "co",
            RedirectUri = TestRedirectUri,
            IsStepUp = isStepUp,
            Phase = phase
        };

    private void SetupSession(PreAuthSession? session)
    {
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>()).Returns(session);
    }

    /// <summary>Guard-path tests use these to prove the handler short-circuited.</summary>
    private async Task AssertExchangeNotAttempted()
    {
        await _exchangeService.DidNotReceiveWithAnyArgs()
            .ExchangeCodeAsync(default!, default!, default!, default, default, default);
    }

    private async Task AssertSessionNotAdvanced()
    {
        await _sessionStore.DidNotReceiveWithAnyArgs()
            .TryAdvanceToCallbackCompletedAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenSessionIdMissing()
    {
        var result = await CreateHandler().Handle(CreateCommand(sessionId: null));

        var forbidden = Assert.IsType<ForbiddenResult<OidcCallbackResponse>>(result);
        Assert.Equal("Missing pre-auth session.", forbidden.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_session"
            && e.Phase == "callback"
            && e.HttpStatus == 403));
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenSessionExpiredOrNotFound()
    {
        SetupSession(null);

        var result = await CreateHandler().Handle(CreateCommand());

        var forbidden = Assert.IsType<ForbiddenResult<OidcCallbackResponse>>(result);
        Assert.Equal("Pre-auth session expired or invalid.", forbidden.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_session"
            && e.Phase == "callback"
            && e.SessionId == TestSessionId
            && e.HttpStatus == 403));
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailure_WhenStateMismatched()
    {
        SetupSession(CreateSession());

        var result = await CreateHandler().Handle(CreateCommand(state: "different-state"));

        var validationFailed = Assert.IsType<ValidationFailedResult<OidcCallbackResponse>>(result);
        Assert.Equal("State parameter mismatch.", validationFailed.Errors.Single().Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "mismatched_state"
            && e.Phase == "callback"
            && e.SessionId == TestSessionId
            && e.HttpStatus == 400));
        await AssertExchangeNotAttempted();
        await AssertSessionNotAdvanced();
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailure_WhenStateMissing()
    {
        // The guard treats a null/empty state the same as a mismatch.
        SetupSession(CreateSession());

        var result = await CreateHandler().Handle(CreateCommand(state: null));

        var validationFailed = Assert.IsType<ValidationFailedResult<OidcCallbackResponse>>(result);
        Assert.Equal("State parameter mismatch.", validationFailed.Errors.Single().Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "mismatched_state" && e.SessionId == TestSessionId));
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Handle_ReturnsPreconditionFailure_WhenSessionAlreadyUsed()
    {
        SetupSession(CreateSession(phase: PreAuthSessionPhase.CallbackCompleted));

        var result = await CreateHandler().Handle(CreateCommand());

        var preconditionFailed = Assert.IsType<PreconditionFailedResult<OidcCallbackResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
        Assert.Equal("Pre-auth session has already been used.", preconditionFailed.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "replay"
            && e.Phase == "callback"
            && e.SessionId == TestSessionId
            && e.HttpStatus == 400));
        await AssertExchangeNotAttempted();
        await AssertSessionNotAdvanced();
    }

    [Fact]
    public async Task Handle_ExchangesWithSessionValues_NeverFromCommand()
    {
        // The code_verifier, redirectUri, and isStepUp must come from the server-side
        // session; nothing in the command can override them (the session says step-up
        // here while the command carries no such field at all).
        SetupSession(CreateSession(isStepUp: true));
        using var cts = new CancellationTokenSource();
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Ok("signed-callback-token"));
        _sessionStore.TryAdvanceToCallbackCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateHandler().Handle(CreateCommand(), cts.Token);

        // Session values win, and the caller's CancellationToken reaches every collaborator.
        await _exchangeService.Received(1).ExchangeCodeAsync(
            "auth-code",
            TestCodeVerifier,
            TestRedirectUri,
            true,
            TestSessionId,
            cts.Token);
        await _sessionStore.Received(1).GetAsync(TestSessionId, cts.Token);
        await _sessionStore.Received(1).TryAdvanceToCallbackCompletedAsync(
            TestSessionId, Arg.Any<string>(), cts.Token);
    }

    [Theory]
    [InlineData(OidcExchangeFailureReason.NotConfigured, DependencyFailedReason.NotConfigured)]
    [InlineData(OidcExchangeFailureReason.DiscoveryUnavailable, DependencyFailedReason.ConnectionFailed)]
    [InlineData(OidcExchangeFailureReason.DiscoveryInvalid, DependencyFailedReason.ConnectionFailed)]
    [InlineData(OidcExchangeFailureReason.ExchangeFailed, DependencyFailedReason.BadRequest)]
    public async Task Handle_MapsExchangeFailureReasons_WhenExchangeFails(
        OidcExchangeFailureReason exchangeReason, DependencyFailedReason expectedReason)
    {
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Fail(exchangeReason, "Exchange failed at IdP."));

        var result = await CreateHandler().Handle(CreateCommand());

        var dependencyFailed = Assert.IsType<DependencyFailedResult<OidcCallbackResponse>>(result);
        Assert.Equal(expectedReason, dependencyFailed.Reason);
        Assert.Equal("Exchange failed at IdP.", dependencyFailed.Message);
        await AssertSessionNotAdvanced();
    }

    [Fact]
    public async Task Handle_ReturnsFallbackError_WhenExchangeFailsWithoutMessage()
    {
        // A failed exchange with no Error set exercises the "Exchange failed." fallback.
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OidcExchangeResult
            {
                Success = false,
                FailureReason = OidcExchangeFailureReason.ExchangeFailed
            });

        var result = await CreateHandler().Handle(CreateCommand());

        var dependencyFailed = Assert.IsType<DependencyFailedResult<OidcCallbackResponse>>(result);
        Assert.Equal(DependencyFailedReason.BadRequest, dependencyFailed.Reason);
        Assert.Equal("Exchange failed.", dependencyFailed.Message);
    }

    [Fact]
    public async Task Handle_ReturnsPreconditionFailure_WhenSessionAdvanceFails()
    {
        // A losing race on the phase transition (another request already advanced
        // the session) is treated as a replay.
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Ok("signed-callback-token"));
        _sessionStore.TryAdvanceToCallbackCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateHandler().Handle(CreateCommand());

        var preconditionFailed = Assert.IsType<PreconditionFailedResult<OidcCallbackResponse>>(result);
        Assert.Equal("Pre-auth session has already been used.", preconditionFailed.Message);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "replay" && e.SessionId == TestSessionId));
    }

    [Fact]
    public async Task Handle_ReturnsCallbackToken_AndStoresTokenHash()
    {
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Ok("signed-callback-token", phoneClaim: "202-555-0100"));
        _sessionStore.TryAdvanceToCallbackCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(CreateCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal("signed-callback-token", result.Value.CallbackToken);
        await _sessionStore.Received(1).TryAdvanceToCallbackCompletedAsync(
            TestSessionId,
            IPreAuthSessionStore.HashCallbackToken("signed-callback-token"),
            Arg.Any<CancellationToken>());
        _callbackFailureLogger.DidNotReceiveWithAnyArgs().Log(default!);
    }
}
