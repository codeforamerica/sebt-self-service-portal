using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SEBT.Portal.Api.Models.IdProofing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Api.Controllers.IdProofing;

/// <summary>
/// Controller for receiving Socure webhook notifications.
/// Anonymous — Socure calls this endpoint, not an authenticated user.
/// Protected by webhook signature validation.
/// </summary>
[ApiController]
[Route("api/socure")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Webhook)]
public class WebhookController(
    SocureSettings socureSettings,
    ILogger<WebhookController> logger) : ControllerBase
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
        var signature = Request.Headers["Authorization"].FirstOrDefault();
        // Strip "Bearer " prefix if present
        var bearerToken = signature?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? signature["Bearer ".Length..]
            : signature;

        var command = new ProcessWebhookCommand
        {
            EventId = payload.EventId ?? string.Empty,
            EvalId = payload.Data?.EvalId,
            ReferenceId = ExtractReferenceId(payload),
            DocumentDecision = ExtractDocumentDecision(payload),
            WebhookSignature = bearerToken
        };

        var result = await handler.Handle(command, cancellationToken);

        // Always return 200 to Socure — failures are logged, not surfaced
        if (!result.IsSuccess)
        {
            logger.LogWarning("Webhook processing returned non-success: {Message}", result.Message);
        }

        return Ok();
    }

    private string? ExtractReferenceId(WebhookPayload payload)
    {
        var docRequest = payload.Data?.DataEnrichments?
            .FirstOrDefault(e => string.Equals(
                e.EnrichmentProvider, socureSettings.DocvEnrichmentName, StringComparison.OrdinalIgnoreCase));

        if (docRequest?.Response == null)
            return null;

        try
        {
            return docRequest.Response.Value.GetProperty("referenceId").GetString();
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string? ExtractDocumentDecision(WebhookPayload payload)
    {
        // The DocV decision is in the enrichment that has a documentVerification property
        var docvEnrichment = payload.Data?.DataEnrichments?
            .FirstOrDefault(e => e.Response != null && HasDocumentVerification(e.Response.Value));

        if (docvEnrichment?.Response == null)
            return null;

        try
        {
            return docvEnrichment.Response.Value
                .GetProperty("documentVerification")
                .GetProperty("decision")
                .GetProperty("value")
                .GetString();
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static bool HasDocumentVerification(JsonElement response)
    {
        return response.TryGetProperty("documentVerification", out _);
    }
}
