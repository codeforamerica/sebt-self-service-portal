using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models.IdProofing;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Api.Controllers.IdProofing;

/// <summary>
/// Controller for receiving Socure webhook notifications.
/// Anonymous — Socure calls this endpoint, not an authenticated user.
/// Protected by webhook signature validation (D11).
/// </summary>
[ApiController]
[Route("api/socure")]
[AllowAnonymous]
public class WebhookController(ILogger<WebhookController> logger) : ControllerBase
{
    /// <summary>
    /// Receives a Socure evaluation_completed webhook.
    /// Always returns 200 OK to prevent Socure retries, even if processing fails internally.
    /// </summary>
    /// <param name="payload">The Socure webhook payload.</param>
    /// <param name="handler">The command handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Webhook received and processed (or acknowledged).</response>
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> HandleWebhook(
        [FromBody] WebhookPayload payload,
        [FromServices] ICommandHandler<ProcessWebhookCommand> handler,
        CancellationToken cancellationToken)
    {
        var signature = Request.Headers["X-Socure-Signature"].FirstOrDefault();

        var command = new ProcessWebhookCommand
        {
            EventId = payload.EventId ?? string.Empty,
            ReferenceId = payload.ReferenceId,
            EvalId = payload.EvalId,
            DocumentDecision = payload.DataEnrichments?.DocumentVerification?.Decision?.Value,
            WebhookSignature = signature
        };

        var result = await handler.Handle(command, cancellationToken);

        // Always return 200 to Socure — failures are logged, not surfaced
        if (!result.IsSuccess)
        {
            logger.LogWarning("Webhook processing returned non-success: {Message}", result.Message);
        }

        return Ok();
    }
}
