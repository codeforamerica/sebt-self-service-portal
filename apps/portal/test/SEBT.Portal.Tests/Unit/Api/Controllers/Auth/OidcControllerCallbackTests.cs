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
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth.OidcCallback;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.Auth;

/// <summary>
/// HTTP-mapping coverage for the callback endpoint: request-shape validation, cookie
/// threading into the command, and result-to-status translation. The orchestration
/// itself is covered by <c>OidcCallbackCommandHandlerTests</c>.
/// </summary>
public class OidcControllerCallbackTests
{
    private const string TestSessionId = "test-session-id";

    private readonly ICommandHandler<OidcCallbackCommand, OidcCallbackResponse> _handler =
        Substitute.For<ICommandHandler<OidcCallbackCommand, OidcCallbackResponse>>();

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
            Substitute.For<IOidcCallbackFailureLogger>(),
            jwtSettings,
            new StateAllowlist(["co"]),
            Substitute.For<IPreAuthSessionStore>(),
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

    private static OidcCallbackRequest CreateRequest(string? code = "auth-code", string? state = "some-state") =>
        new(code, state, StateCode: null);

    private void SetupResult(Result<OidcCallbackResponse> result)
    {
        _handler.Handle(Arg.Any<OidcCallbackCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenBodyIsNull()
    {
        var controller = CreateController();

        var result = await controller.Callback(null, _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Missing code.", error.Error);
        await _handler.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenCodeMissing()
    {
        var controller = CreateController();

        var result = await controller.Callback(CreateRequest(code: null), _handler, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Missing code.", error.Error);
        await _handler.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task Callback_PassesBodyAndSessionCookieToHandler()
    {
        var controller = CreateController();
        SetupResult(Result<OidcCallbackResponse>.Success(
            new OidcCallbackResponse("signed-callback-token")));
        using var cts = new CancellationTokenSource();

        await controller.Callback(CreateRequest(), _handler, cts.Token);

        await _handler.Received(1).Handle(
            Arg.Is<OidcCallbackCommand>(c =>
                c.Code == "auth-code" && c.State == "some-state" && c.SessionId == TestSessionId),
            cts.Token);
    }

    [Fact]
    public async Task Callback_PassesNullSessionId_WhenCookieMissing()
    {
        var controller = CreateController(withSessionCookie: false);
        SetupResult(Result<OidcCallbackResponse>.Forbidden("Missing pre-auth session."));

        await controller.Callback(CreateRequest(), _handler, CancellationToken.None);

        await _handler.Received(1).Handle(
            Arg.Is<OidcCallbackCommand>(c => c.SessionId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Callback_ReturnsOkWithCallbackToken_WhenHandlerSucceeds()
    {
        var controller = CreateController();
        SetupResult(Result<OidcCallbackResponse>.Success(
            new OidcCallbackResponse("signed-callback-token")));

        var result = await controller.Callback(CreateRequest(), _handler, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        // The success body is an anonymous type; read the property via reflection.
        var callbackToken = okResult.Value!.GetType().GetProperty("callbackToken")!.GetValue(okResult.Value);
        Assert.Equal("signed-callback-token", callbackToken);
    }

    [Fact]
    public async Task Callback_ReturnsForbidden_WhenHandlerForbids()
    {
        var controller = CreateController();
        SetupResult(Result<OidcCallbackResponse>.Forbidden("Missing pre-auth session."));

        var result = await controller.Callback(CreateRequest(), _handler, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Missing pre-auth session.", error.Error);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WithValidationFailureMessage()
    {
        var controller = CreateController();
        SetupResult(Result<OidcCallbackResponse>.ValidationFailed("state", "State parameter mismatch."));

        var result = await controller.Callback(CreateRequest(), _handler, CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("State parameter mismatch.", error.Error);
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WithPreconditionFailureMessage()
    {
        var controller = CreateController();
        SetupResult(Result<OidcCallbackResponse>.PreconditionFailed(
            PreconditionFailedReason.Conflict, "Pre-auth session has already been used."));

        var result = await controller.Callback(CreateRequest(), _handler, CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Pre-auth session has already been used.", error.Error);
    }

    [Theory]
    [InlineData(DependencyFailedReason.NotConfigured, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(DependencyFailedReason.ConnectionFailed, StatusCodes.Status502BadGateway)]
    [InlineData(DependencyFailedReason.BadRequest, StatusCodes.Status400BadRequest)]
    public async Task Callback_MapsDependencyFailuresToStatusCodes(
        DependencyFailedReason reason, int expectedStatus)
    {
        var controller = CreateController();
        SetupResult(Result<OidcCallbackResponse>.DependencyFailed(reason, "Exchange failed at IdP."));

        var result = await controller.Callback(CreateRequest(), _handler, CancellationToken.None);

        // BadRequest() returns the BadRequestObjectResult subtype, so assert assignability.
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("Exchange failed at IdP.", error.Error);
    }
}
