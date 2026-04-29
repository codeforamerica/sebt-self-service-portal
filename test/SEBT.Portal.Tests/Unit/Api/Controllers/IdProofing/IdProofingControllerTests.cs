using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SEBT.Portal.Api.Controllers.IdProofing;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Api.Models.IdProofing;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.IdProofing;

public class IdProofingControllerTests
{
    private readonly ICommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse> _handler =
        Substitute.For<ICommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse>>();

    private readonly ILogger<IdProofingController> _logger =
        Substitute.For<ILogger<IdProofingController>>();

    private static SubmitIdProofingRequest BuildRequest() =>
        new(
            DateOfBirth: new DateOfBirthDto(Month: "03", Day: "15", Year: "1990"),
            IdType: "ssn",
            IdValue: "999-99-9999",
            DiSessionToken: null);

    private IdProofingController BuildController(HttpContext httpContext)
    {
        httpContext.Items[ResolveUserFilter.UserIdKey] = Guid.NewGuid();
        return new IdProofingController(_logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task Submit_LogsRemoteIpAndXForwardedFor_WhenHeaderPresent()
    {
        // Boundary log lets us see the full XFF chain that reaches the API in deployed
        // envs. Without it we cannot pick the right ForwardLimit value, since AWS ALB +
        // CloudFront append entries silently and the chain depth is not visible from
        // outside the VPC.
        _handler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.Success(new SubmitIdProofingResponse(Result: "matched")));

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.55");
        httpContext.Request.Headers["X-Forwarded-For"] =
            "203.0.113.10, 198.51.100.7, 10.0.0.42";

        var controller = BuildController(httpContext);

        await controller.Submit(BuildRequest(), _handler, CancellationToken.None);

        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("10.0.0.55") &&
                o.ToString()!.Contains("203.0.113.10, 198.51.100.7, 10.0.0.42")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Submit_LogsAbsentMarker_WhenXForwardedForMissing()
    {
        // Local-dev case: nothing in front of the API, no upstream proxy sets XFF.
        // Marker keeps the structured log queryable without ambiguous empty strings.
        _handler.Handle(Arg.Any<SubmitIdProofingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmitIdProofingResponse>.Success(new SubmitIdProofingResponse(Result: "matched")));

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

        var controller = BuildController(httpContext);

        await controller.Submit(BuildRequest(), _handler, CancellationToken.None);

        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("127.0.0.1") &&
                o.ToString()!.Contains("<absent>")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
