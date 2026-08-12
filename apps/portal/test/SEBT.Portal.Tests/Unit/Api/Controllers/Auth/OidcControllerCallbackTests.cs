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
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.Auth;

public class OidcControllerCallbackTests
{
    private const string TestSessionId = "test-session-id";
    private const string TestState = "expected-state";
    private const string TestCodeVerifier = "test-code-verifier";
    private const string TestRedirectUri = "http://localhost:3000/callback";

    private readonly IOidcCallbackFailureLogger _callbackFailureLogger =
        Substitute.For<IOidcCallbackFailureLogger>();

    private readonly IPreAuthSessionStore _sessionStore = Substitute.For<IPreAuthSessionStore>();
    private readonly IOidcExchangeService _exchangeService = Substitute.For<IOidcExchangeService>();

    private OidcController CreateController(bool withSessionCookie = true)
    {
        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });

        var controller = new OidcController(
            Substitute.For<IConfiguration>(),
            NullLogger<OidcController>.Instance,
            _callbackFailureLogger,
            Substitute.For<IUserRepository>(),
            Substitute.For<IOidcTokenService>(),
            jwtSettings,
            new StateAllowlist(["co"]),
            _sessionStore,
            Substitute.For<IWebHostEnvironment>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        if (withSessionCookie)
        {
            controller.ControllerContext.HttpContext.Request.Headers.Cookie =
                $"{OidcSessionCookie.CookieName}={TestSessionId}";
        }

        return controller;
    }

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

    private static OidcCallbackRequest CreateRequest(string? code = "auth-code", string? state = TestState) =>
        new(code, state, StateCode: null);

    private void SetupSession(PreAuthSession? session)
    {
        _sessionStore.GetAsync(TestSessionId, Arg.Any<CancellationToken>()).Returns(session);
    }

    /// <summary>Guard-path tests use these to prove the action short-circuited.</summary>
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
    public async Task Callback_ReturnsBadRequest_WhenBodyIsNull()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.Callback(null, _exchangeService, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Missing code.", error.Error);
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenCodeMissing()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.Callback(CreateRequest(code: null), _exchangeService, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Missing code.", error.Error);
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Callback_ReturnsForbidden_WhenSessionCookieMissing()
    {
        // Arrange
        var controller = CreateController(withSessionCookie: false);

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Missing pre-auth session.", error.Error);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_session"
            && e.Phase == "callback"
            && e.HttpStatus == StatusCodes.Status403Forbidden));
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Callback_ReturnsForbidden_WhenSessionExpiredOrNotFound()
    {
        // Arrange
        var controller = CreateController();
        SetupSession(null);

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Pre-auth session expired or invalid.", error.Error);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "missing_session"
            && e.Phase == "callback"
            && e.SessionId == TestSessionId
            && e.HttpStatus == StatusCodes.Status403Forbidden));
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenStateMismatched()
    {
        // Arrange
        var controller = CreateController();
        SetupSession(CreateSession());

        // Act
        var result = await controller.Callback(
            CreateRequest(state: "different-state"), _exchangeService, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("State parameter mismatch.", error.Error);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "mismatched_state"
            && e.Phase == "callback"
            && e.SessionId == TestSessionId
            && e.HttpStatus == StatusCodes.Status400BadRequest));
        await AssertExchangeNotAttempted();
        await AssertSessionNotAdvanced();
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenStateMissing()
    {
        // Arrange — the guard treats a null/empty state the same as a mismatch.
        var controller = CreateController();
        SetupSession(CreateSession());

        // Act
        var result = await controller.Callback(
            CreateRequest(state: null), _exchangeService, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("State parameter mismatch.", error.Error);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "mismatched_state" && e.SessionId == TestSessionId));
        await AssertExchangeNotAttempted();
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenSessionAlreadyUsed()
    {
        // Arrange
        var controller = CreateController();
        SetupSession(CreateSession(phase: PreAuthSessionPhase.CallbackCompleted));

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Pre-auth session has already been used.", error.Error);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "replay"
            && e.Phase == "callback"
            && e.SessionId == TestSessionId
            && e.HttpStatus == StatusCodes.Status400BadRequest));
        await AssertExchangeNotAttempted();
        await AssertSessionNotAdvanced();
    }

    [Fact]
    public async Task Callback_ExchangesWithSessionValues_NeverFromBody()
    {
        // The code_verifier, redirectUri, and isStepUp must come from the server-side
        // session; the body's isStepUp is ignored (it stays false here while the
        // session says true).
        var controller = CreateController();
        SetupSession(CreateSession(isStepUp: true));
        using var cts = new CancellationTokenSource();
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Ok("signed-callback-token"));
        _sessionStore.TryAdvanceToCallbackCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await controller.Callback(CreateRequest(), _exchangeService, cts.Token);

        // Assert — session values win, and the caller's CancellationToken reaches
        // every collaborator.
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

    [Fact]
    public async Task Callback_ReturnsExchangeStatusAndError_WhenExchangeFails()
    {
        // Arrange
        var controller = CreateController();
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Fail("Exchange failed at IdP.", StatusCodes.Status502BadGateway));

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Exchange failed at IdP.", error.Error);
        await AssertSessionNotAdvanced();
    }

    [Fact]
    public async Task Callback_ReturnsFallbackError_WhenExchangeFailsWithoutMessage()
    {
        // Arrange — a failed exchange with no Error set exercises the
        // "Exchange failed." fallback message branch.
        var controller = CreateController();
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new OidcExchangeResult { Success = false, StatusCode = StatusCodes.Status400BadRequest });

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Exchange failed.", error.Error);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenSessionAdvanceFails()
    {
        // A losing race on the phase transition (another request already advanced
        // the session) is treated as a replay.
        var controller = CreateController();
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Ok("signed-callback-token"));
        _sessionStore.TryAdvanceToCallbackCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Pre-auth session has already been used.", error.Error);
        _callbackFailureLogger.Received(1).Log(Arg.Is<OidcCallbackFailureLogEntry>(e =>
            e.Reason == "replay" && e.SessionId == TestSessionId));
    }

    [Fact]
    public async Task Callback_ReturnsOkWithCallbackToken_AndStoresTokenHash()
    {
        // Arrange
        var controller = CreateController();
        SetupSession(CreateSession());
        _exchangeService.ExchangeCodeAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OidcExchangeResult.Ok("signed-callback-token"));
        _sessionStore.TryAdvanceToCallbackCompletedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await controller.Callback(CreateRequest(), _exchangeService, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        // The success body is an anonymous type; read the property via reflection.
        var callbackToken = okResult.Value!.GetType().GetProperty("callbackToken")!.GetValue(okResult.Value);
        Assert.Equal("signed-callback-token", callbackToken);
        await _sessionStore.Received(1).TryAdvanceToCallbackCompletedAsync(
            TestSessionId,
            IPreAuthSessionStore.HashCallbackToken("signed-callback-token"),
            Arg.Any<CancellationToken>());
    }
}
