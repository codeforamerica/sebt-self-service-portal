using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers.IdProofing;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Api.Models.IdProofing;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.IdProofing;

public class IdProofingControllerTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly ICommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse> _submitHandler =
        Substitute.For<ICommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse>>();

    private readonly IQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse> _statusHandler =
        Substitute.For<IQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse>>();

    /// <summary>
    /// Builds the controller with HttpContext.Items pre-seeded the way ResolveUserFilter
    /// would in a live host, so actions can read the resolved user ID.
    /// </summary>
    private static IdProofingController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[ResolveUserFilter.UserIdKey] = TestUserId;

        return new IdProofingController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static SubmitIdProofingRequest CreateSubmitRequest() =>
        new(new DateOfBirthDto("03", "15", "1990"), "ssn", "999-99-9999", "di-session-token");

    [Fact]
    public void Controller_RequiresAuthorizationAndResolvedUser()
    {
        // Attribute enforcement only runs in a host; presence asserts pin the
        // guards against accidental removal.
        var attributes = typeof(IdProofingController).GetCustomAttributes(inherit: true);

        Assert.Contains(attributes, a => a is AuthorizeAttribute);
        Assert.Contains(attributes.OfType<ServiceFilterAttribute>(),
            f => f.ServiceType == typeof(ResolveUserFilter));
    }

    [Fact]
    public async Task Submit_ReturnsOkWithResponse_WhenHandlerSucceeds()
    {
        // Arrange
        var controller = CreateController();
        var response = new SubmitIdProofingResponse("matched");
        _submitHandler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.Success(response));

        // Act
        var result = await controller.Submit(CreateSubmitRequest(), _submitHandler, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task Submit_MapsRequestToCommand()
    {
        // Arrange
        var controller = CreateController();
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        using var cts = new CancellationTokenSource();
        _submitHandler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.Success(new SubmitIdProofingResponse("matched")));

        // Act
        await controller.Submit(CreateSubmitRequest(), _submitHandler, cts.Token);

        // Assert — includes the caller's CancellationToken reaching the handler.
        await _submitHandler.Received(1).Handle(
            Arg.Is<SubmitIdProofingCommand>(c =>
                c.UserId == TestUserId
                && c.DateOfBirth == "1990-03-15"
                && c.IdType == "ssn"
                && c.IdValue == "999-99-9999"
                && c.DiSessionToken == "di-session-token"
                && c.IpAddress == "203.0.113.7"),
            cts.Token);
    }

    [Fact]
    public async Task Submit_MapsNullIpAddress_WhenRemoteIpUnavailable()
    {
        // RemoteIpAddress is null behind some reverse-proxy setups; the command
        // must carry null rather than throw.
        var controller = CreateController();
        _submitHandler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.Success(new SubmitIdProofingResponse("matched")));

        // Act
        await controller.Submit(CreateSubmitRequest(), _submitHandler, CancellationToken.None);

        // Assert
        await _submitHandler.Received(1).Handle(
            Arg.Is<SubmitIdProofingCommand>(c => c.IpAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ZeroPadsDateOfBirthComponents()
    {
        // The frontend sends zero-padded components, but the controller pads
        // defensively; a single-digit month/day must still produce yyyy-MM-dd.
        var controller = CreateController();
        var request = new SubmitIdProofingRequest(new DateOfBirthDto("1", "5", "2020"), "ssn", "999-99-9999");
        _submitHandler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.Success(new SubmitIdProofingResponse("matched")));

        // Act
        await controller.Submit(request, _submitHandler, CancellationToken.None);

        // Assert
        await _submitHandler.Received(1).Handle(
            Arg.Is<SubmitIdProofingCommand>(c => c.DateOfBirth == "2020-01-05"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var controller = CreateController();
        _submitHandler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.ValidationFailed("IdValue", "ID value is invalid."));

        // Act
        var result = await controller.Submit(CreateSubmitRequest(), _submitHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task Submit_ReturnsNotFound_WhenUserNotFound()
    {
        // Arrange
        var controller = CreateController();
        _submitHandler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found."));

        // Act
        var result = await controller.Submit(CreateSubmitRequest(), _submitHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task GetStatus_ReturnsOkWithResponse_WhenHandlerSucceeds()
    {
        // Arrange
        var controller = CreateController();
        var response = new VerificationStatusResponse("verified");
        _statusHandler.Handle(Arg.Any<GetVerificationStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationStatusResponse>.Success(response));

        // Act
        var result = await controller.GetStatus(Guid.NewGuid(), _statusHandler, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task GetStatus_MapsChallengeIdAndUserIdToQuery()
    {
        // Arrange
        var controller = CreateController();
        var challengeId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _statusHandler.Handle(Arg.Any<GetVerificationStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationStatusResponse>.Success(new VerificationStatusResponse("pending")));

        // Act
        await controller.GetStatus(challengeId, _statusHandler, cts.Token);

        // Assert — includes the caller's CancellationToken reaching the handler.
        await _statusHandler.Received(1).Handle(
            Arg.Is<GetVerificationStatusQuery>(q =>
                q.ChallengeId == challengeId
                && q.UserId == TestUserId),
            cts.Token);
    }

    [Fact]
    public async Task GetStatus_ReturnsNotFound_WhenChallengeNotFound()
    {
        // Arrange
        var controller = CreateController();
        _statusHandler.Handle(Arg.Any<GetVerificationStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<VerificationStatusResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "Challenge not found."));

        // Act
        var result = await controller.GetStatus(Guid.NewGuid(), _statusHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.IsType<ProblemDetails>(objectResult.Value);
    }
}
