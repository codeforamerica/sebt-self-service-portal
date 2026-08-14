using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Diagnostics;

namespace SEBT.Portal.Api.Controllers.Diagnostics;

/// <summary>
/// Diagnostic endpoints for validating OTEL tracing, structured logging, and error-handling behavior.
/// All endpoints are gated by the <c>test_error_endpoints_enabled</c> feature flag (off by default).
/// Enable in appsettings.Development.json or AWS AppConfig for dev/staging. Do not enable in production.
/// </summary>
[ApiController]
[Route("api/test-error")]
[AllowAnonymous]
[FeatureGate(FeatureFlags.TestErrorEndpointsEnabled)]
[Tags("Diagnostics")]
public class TestErrorController : ControllerBase
{
    /// <summary>
    /// Returns the specified HTTP status code without going through a command handler.
    /// Useful for testing frontend error-handling branches in isolation.
    /// </summary>
    [HttpGet("http/{statusCode:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HttpStatus([FromRoute] int statusCode)
    {
        return StatusCode(statusCode);
    }

    /// <summary>
    /// Throws an unhandled <see cref="InvalidOperationException"/> inside <see cref="TestErrorCommandHandler"/>.
    /// Validates that OTEL spans are marked faulted and the exception is captured in structured logs.
    /// </summary>
    [HttpGet("exception")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ThrowException(
        [FromServices] ICommandHandler<TestErrorCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.Handle(new TestErrorCommand(), cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Simulates a timeout: <see cref="TestErrorCommandHandler"/> delays 30 seconds;
    /// the controller cancels after 500 ms. Validates that <see cref="OperationCanceledException"/>
    /// propagates correctly through OTEL spans and structured logging.
    /// </summary>
    [HttpGet("exception/timeout")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SimulateTimeout(
        [FromServices] ICommandHandler<TestErrorCommand> handler)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await handler.Handle(new TestErrorCommand { WithDelay = true }, cts.Token);
        return Ok();
    }

    /// <summary>
    /// Exercises the Smarty address verification error path for a server-error HTTP response.
    /// <see cref="IAddressVerificationDiagnostics"/> runs the real verification service against a
    /// canned 500 response, so its <c>LogError</c> and <c>DependencyFailed</c> result are
    /// exercised without any network activity.
    /// </summary>
    [HttpGet("dependencies/smarty/http-error")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SimulateSmartyHttpError(
        [FromServices] IAddressVerificationDiagnostics diagnostics,
        CancellationToken cancellationToken)
    {
        await diagnostics.ValidateAgainstCannedServerErrorAsync(cancellationToken);

        return Accepted();
    }

    /// <summary>
    /// Exercises the Smarty address verification error path for a transport-level failure
    /// (e.g. firewall block, DNS failure). <see cref="IAddressVerificationDiagnostics"/> simulates
    /// an <see cref="HttpRequestException"/>, which the verification service catches and logs
    /// at <c>Error</c>.
    /// </summary>
    [HttpGet("dependencies/smarty/transport-failure")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SimulateSmartyTransportFailure(
        [FromServices] IAddressVerificationDiagnostics diagnostics,
        CancellationToken cancellationToken)
    {
        await diagnostics.ValidateAgainstTransportFailureAsync(cancellationToken);

        return Accepted();
    }
}
