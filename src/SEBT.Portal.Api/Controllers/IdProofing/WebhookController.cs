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
            EventType = payload.EventType,
            EvalId = payload.Data?.EvalId,
            ReferenceId = ExtractReferenceId(payload),
            // Top-level workflow decision is authoritative for routing (DC-296).
            WorkflowDecision = payload.Data?.Decision,
            // DocV enrichment decision is kept for diagnostic logging, not routing.
            DocumentDecision = ExtractDocumentDecision(payload),
            WebhookSignature = bearerToken
        };

        // Boundary log: capture what arrived before correlation/dispatch runs. Lets us trace
        // every webhook by event_id even if downstream branches return early. Source contract is
        // fragile, so we log the raw deserialized fields side-by-side with the mapped command.
        // All values are sanitized — webhook payload is attacker-controllable input (CWE-117).
        logger.LogInformation(
            "Webhook received: EventId={EventId}, EventType={EventType}, EvalId={EvalId}, " +
            "ReferenceId={ReferenceId}, WorkflowDecision={WorkflowDecision}, " +
            "DocumentDecision={DocumentDecision}",
            SanitizeForLog(command.EventId),
            SanitizeForLog(command.EventType),
            SanitizeForLog(command.EvalId),
            SanitizeForLog(command.ReferenceId),
            SanitizeForLog(command.WorkflowDecision),
            SanitizeForLog(command.DocumentDecision));

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

    /// <summary>
    /// Strips CR/LF from values logged from the webhook payload to prevent log forging
    /// (CWE-117). Mirrors the sanitizer in <c>ProcessWebhookCommandHandler</c>.
    /// </summary>
    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}
