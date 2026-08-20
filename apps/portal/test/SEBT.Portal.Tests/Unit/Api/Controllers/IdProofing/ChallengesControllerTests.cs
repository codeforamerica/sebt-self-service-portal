using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers.IdProofing;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.IdProofing;

public class ChallengesControllerTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly ICommandHandler<StartChallengeCommand, StartChallengeResponse> _startHandler =
        Substitute.For<ICommandHandler<StartChallengeCommand, StartChallengeResponse>>();

    private readonly ICommandHandler<ResubmitChallengeCommand, ResubmitChallengeResponse> _resubmitHandler =
        Substitute.For<ICommandHandler<ResubmitChallengeCommand, ResubmitChallengeResponse>>();

    /// <summary>
    /// Builds the controller with HttpContext.Items pre-seeded the way ResolveUserFilter
    /// would in a live host, so actions can read the resolved user ID.
    /// </summary>
    private static ChallengesController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[ResolveUserFilter.UserIdKey] = TestUserId;

        return new ChallengesController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public void Controller_RequiresAuthorizationAndResolvedUser()
    {
        // Attribute enforcement only runs in a host; presence asserts pin the
        // guards against accidental removal.
        var attributes = typeof(ChallengesController).GetCustomAttributes(inherit: true);

        Assert.Contains(attributes, a => a is AuthorizeAttribute);
        Assert.Contains(attributes.OfType<ServiceFilterAttribute>(),
            f => f.ServiceType == typeof(ResolveUserFilter));
    }

    [Fact]
    public async Task Start_ReturnsOkWithResponse_WhenHandlerSucceeds()
    {
        // Arrange
        var controller = CreateController();
        var response = new StartChallengeResponse("docv-token", "https://docv.example/session");
        _startHandler.Handle(Arg.Any<StartChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartChallengeResponse>.Success(response));

        // Act
        var result = await controller.Start(Guid.NewGuid(), _startHandler, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task Start_MapsChallengeIdAndUserIdToCommand()
    {
        // Arrange
        var controller = CreateController();
        var challengeId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _startHandler.Handle(Arg.Any<StartChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartChallengeResponse>.Success(
                new StartChallengeResponse("docv-token", "https://docv.example/session")));

        // Act
        await controller.Start(challengeId, _startHandler, cts.Token);

        // Assert — includes the caller's CancellationToken reaching the handler.
        await _startHandler.Received(1).Handle(
            Arg.Is<StartChallengeCommand>(c =>
                c.ChallengeId == challengeId
                && c.UserId == TestUserId),
            cts.Token);
    }

    [Fact]
    public async Task Start_ReturnsNotFound_WhenChallengeNotFound()
    {
        // Arrange
        var controller = CreateController();
        _startHandler.Handle(Arg.Any<StartChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "Challenge not found."));

        // Act
        var result = await controller.Start(Guid.NewGuid(), _startHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Start_ReturnsConflict_WhenChallengeNotStartable()
    {
        // Arrange
        var controller = CreateController();
        _startHandler.Handle(Arg.Any<StartChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Challenge is not in a startable state."));

        // Act
        var result = await controller.Start(Guid.NewGuid(), _startHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task Start_ReturnsBadGateway_WhenDocvSessionCreationFails()
    {
        // Arrange
        var controller = CreateController();
        _startHandler.Handle(Arg.Any<StartChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartChallengeResponse>.DependencyFailed(
                DependencyFailedReason.ServiceUnavailable, "DocV session creation failed."));

        // Act
        var result = await controller.Start(Guid.NewGuid(), _startHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    }

    [Fact]
    public async Task Resubmit_ReturnsOkWithResponse_WhenHandlerSucceeds()
    {
        // Arrange
        var controller = CreateController();
        var response = new ResubmitChallengeResponse(
            Guid.NewGuid(), "docv-token", "https://docv.example/session");
        _resubmitHandler.Handle(Arg.Any<ResubmitChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ResubmitChallengeResponse>.Success(response));

        // Act
        var result = await controller.Resubmit(Guid.NewGuid(), _resubmitHandler, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task Resubmit_MapsChallengeIdAndUserIdToCommand()
    {
        // Arrange
        var controller = CreateController();
        var challengeId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _resubmitHandler.Handle(Arg.Any<ResubmitChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ResubmitChallengeResponse>.Success(
                new ResubmitChallengeResponse(Guid.NewGuid(), "docv-token", "https://docv.example/session")));

        // Act
        await controller.Resubmit(challengeId, _resubmitHandler, cts.Token);

        // Assert — includes the caller's CancellationToken reaching the handler.
        await _resubmitHandler.Received(1).Handle(
            Arg.Is<ResubmitChallengeCommand>(c =>
                c.ChallengeId == challengeId
                && c.UserId == TestUserId),
            cts.Token);
    }

    [Fact]
    public async Task Resubmit_ReturnsNotFound_WhenChallengeNotFound()
    {
        // Arrange
        var controller = CreateController();
        _resubmitHandler.Handle(Arg.Any<ResubmitChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "Challenge not found."));

        // Act
        var result = await controller.Resubmit(Guid.NewGuid(), _resubmitHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Resubmit_ReturnsConflict_WhenChallengeNotInResubmitState()
    {
        // Arrange
        var controller = CreateController();
        _resubmitHandler.Handle(Arg.Any<ResubmitChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Challenge is not in Resubmit state."));

        // Act
        var result = await controller.Resubmit(Guid.NewGuid(), _resubmitHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task Resubmit_ReturnsBadGateway_WhenStepUpCallFails()
    {
        // Arrange
        var controller = CreateController();
        _resubmitHandler.Handle(Arg.Any<ResubmitChallengeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ResubmitChallengeResponse>.DependencyFailed(
                DependencyFailedReason.ServiceUnavailable, "Socure step-up call failed."));

        // Act
        var result = await controller.Resubmit(Guid.NewGuid(), _resubmitHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    }
}
