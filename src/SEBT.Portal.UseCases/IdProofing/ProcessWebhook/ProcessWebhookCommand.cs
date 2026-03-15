using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Command to process an incoming Socure webhook notification.
/// The webhook is received anonymously and verified by signature.
/// </summary>
public class ProcessWebhookCommand : ICommand
{
    /// <summary>
    /// The Socure event ID. Used for idempotency — if already processed, return success.
    /// </summary>
    [Required(ErrorMessage = "EventId is required.")]
    public string EventId { get; init; } = string.Empty;

    /// <summary>
    /// The Socure reference ID for challenge correlation (primary key).
    /// </summary>
    public string? ReferenceId { get; init; }

    /// <summary>
    /// The Socure evaluation ID for challenge correlation (fallback key).
    /// </summary>
    public string? EvalId { get; init; }

    /// <summary>
    /// The document verification decision value from Socure's data_enrichments.
    /// Expected values: "accept", "reject", or similar Socure-defined outcomes.
    /// </summary>
    public string? DocumentDecision { get; init; }

    /// <summary>
    /// The raw webhook signature header for validation.
    /// Placeholder validation in dev; enforced in non-dev.
    /// </summary>
    public string? WebhookSignature { get; init; }
}
