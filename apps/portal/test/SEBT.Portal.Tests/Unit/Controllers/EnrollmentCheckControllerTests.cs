using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers.EnrollmentCheck;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class EnrollmentCheckControllerTests
{
    private readonly ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckOutcome> _handler =
        Substitute.For<ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckOutcome>>();

    [Fact]
    public async Task CheckEnrollment_WithValidRequest_ReturnsOk()
    {
        var controller = new EnrollmentCheckController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        _handler.Handle(Arg.Any<CheckEnrollmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentCheckOutcome>.Success(new EnrollmentCheckOutcome(
                [
                    new EnrollmentChildOutcome(
                        Guid.NewGuid(), "Jane", "Doe", new DateOnly(2015, 3, 12), IsMatch: true)
                ])));

        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "2015-03-12",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var result = await controller.CheckEnrollment(_handler, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<EnrollmentCheckApiResponse>(okResult.Value);
        Assert.Single(response.Results);
        Assert.Equal("Match", response.Results[0].Status);
    }

    [Fact]
    public async Task CheckEnrollment_WithInvalidDateFormat_ReturnsBadRequest()
    {
        var controller = new EnrollmentCheckController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "not-a-date",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var result = await controller.CheckEnrollment(_handler, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CheckEnrollment_WhenBackendFails_Returns503()
    {
        var controller = new EnrollmentCheckController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        _handler.Handle(Arg.Any<CheckEnrollmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentCheckOutcome>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Service unavailable."));

        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "2015-03-12",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var result = await controller.CheckEnrollment(_handler, request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
        Assert.IsType<ProblemDetails>(statusResult.Value);
    }
}
