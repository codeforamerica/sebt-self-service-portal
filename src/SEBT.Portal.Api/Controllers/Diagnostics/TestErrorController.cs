using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Infrastructure.Services;
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
    /// Exercises the <see cref="SmartyAddressUpdateService"/> error path for a non-2xx HTTP response.
    /// Constructs the real service with a fake <see cref="HttpMessageHandler"/> that returns 500,
    /// so the handler's <c>LogError</c> and <c>DependencyFailed</c> result are exercised without
    /// any network activity.
    /// </summary>
    [HttpGet("dependencies/smarty/http-error")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SimulateSmartyHttpError(
        [FromServices] IOptionsSnapshot<SmartySettings> smartySettings,
        [FromServices] IOptionsSnapshot<AddressValidationPolicySettings> policySettings,
        [FromServices] ILogger<SmartyAddressUpdateService> logger,
        CancellationToken cancellationToken)
    {
        await InvokeSmartyService(
            new FixedResponseHandler(HttpStatusCode.InternalServerError),
            smartySettings, policySettings, logger, cancellationToken);

        return Accepted();
    }

    /// <summary>
    /// Exercises the <see cref="SmartyAddressUpdateService"/> error path for a transport-level failure
    /// (e.g. firewall block, DNS failure). The fake handler throws <see cref="HttpRequestException"/>,
    /// which the service catches and logs at <c>Error</c>.
    /// </summary>
    [HttpGet("dependencies/smarty/transport-failure")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SimulateSmartyTransportFailure(
        [FromServices] IOptionsSnapshot<SmartySettings> smartySettings,
        [FromServices] IOptionsSnapshot<AddressValidationPolicySettings> policySettings,
        [FromServices] ILogger<SmartyAddressUpdateService> logger,
        CancellationToken cancellationToken)
    {
        await InvokeSmartyService(
            new FixedResponseHandler(transportFailure: true),
            smartySettings, policySettings, logger, cancellationToken);

        return Accepted();
    }

    private static Task InvokeSmartyService(
        FixedResponseHandler handler,
        IOptionsSnapshot<SmartySettings> smartySettings,
        IOptionsSnapshot<AddressValidationPolicySettings> policySettings,
        ILogger<SmartyAddressUpdateService> logger,
        CancellationToken cancellationToken)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://us-street.api.smartystreets.com/")
        };
        var factory = new SingleClientFactory(client);
        var service = new SmartyAddressUpdateService(factory, smartySettings, policySettings, logger);

        return service.ValidateAndNormalizeAsync(new AddressUpdateOperationRequest
        {
            StreetAddress1 = "123 Main St",
            City = "Denver",
            State = "CO",
            PostalCode = "80203"
        }, cancellationToken);
    }

    private sealed class FixedResponseHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        bool transportFailure = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (transportFailure)
                throw new HttpRequestException("Simulated transport failure: connection refused.");

            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
