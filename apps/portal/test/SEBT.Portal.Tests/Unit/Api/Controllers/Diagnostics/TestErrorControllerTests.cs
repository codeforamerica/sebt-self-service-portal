using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Diagnostics;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.UseCases.Diagnostics;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.Diagnostics;

public class TestErrorControllerTests
{
    private static TestErrorController CreateController() =>
        new() { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static TestErrorCommandHandler CreateRealHandler() =>
        new(NullLogger<TestErrorCommandHandler>.Instance);

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public void HttpStatus_ReturnsRequestedStatusCode(int statusCode)
    {
        var result = CreateController().HttpStatus(statusCode);

        var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(statusCode, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task ThrowException_PropagatesHandlerException()
    {
        var controller = CreateController();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.ThrowException(CreateRealHandler(), CancellationToken.None));
    }

    [Fact]
    public async Task SimulateTimeout_PropagatesCancellation()
    {
        // The action cancels its own 500 ms token while the handler simulates a 30 s dependency.
        var controller = CreateController();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.SimulateTimeout(CreateRealHandler()));
    }

    [Fact]
    public async Task SimulateSmartyHttpError_ReturnsAccepted()
    {
        var diagnostics = Substitute.For<IAddressVerificationDiagnostics>();
        var controller = CreateController();

        var result = await controller.SimulateSmartyHttpError(
            diagnostics,
            CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        await diagnostics.Received(1).ValidateAgainstCannedServerErrorAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SimulateSmartyTransportFailure_ReturnsAccepted()
    {
        var diagnostics = Substitute.For<IAddressVerificationDiagnostics>();
        var controller = CreateController();

        var result = await controller.SimulateSmartyTransportFailure(
            diagnostics,
            CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        await diagnostics.Received(1).ValidateAgainstTransportFailureAsync(CancellationToken.None);
    }
}
