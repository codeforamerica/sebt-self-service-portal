using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;
using SEBT.Portal.Core.AppSettings;
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
    /// Simulates a Smarty transport-level failure. Uses the "Smarty" named <see cref="HttpClient"/>
    /// (so OTEL produces a correctly-labeled dependency span) but cancels the request immediately
    /// via a pre-cancelled token — no bytes leave the process.
    /// </summary>
    [HttpGet("dependencies/smarty")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SimulateSmartyFailure(
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("Smarty");
        try
        {
            // Pre-cancelled token: HttpClient fails before any socket activity, but the OTEL
            // HttpClient instrumentation still records the span and marks it as cancelled/error.
            await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, client.BaseAddress ?? new Uri("https://us-street.api.smartystreets.com/")),
                new CancellationToken(canceled: true));
        }
        catch (OperationCanceledException) { }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Title = "Address verification service is temporarily unavailable.",
            Status = StatusCodes.Status503ServiceUnavailable
        });
    }
}
