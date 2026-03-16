using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Command to process an incoming Socure webhook notification.
/// The webhook is received anonymously and verified by signature.
/// </summary>
public class ProcessWebhookCommand : ICommand
{
    // Control characters in webhook fields enable log injection (forged log entries).
    // Reject them at validation so they never reach log call sites.
    private const string NoControlChars = @"^[^\p{Cc}]*$";
    private const string ControlCharError = "Field contains invalid control characters.";

    /// <summary>
    /// The Socure event ID. Used for idempotency — if already processed, return success.
    /// </summary>
    [Required(ErrorMessage = "EventId is required.")]
    [RegularExpression(NoControlChars, ErrorMessage = ControlCharError)]
    public string EventId { get; init; } = string.Empty;

    /// <summary>
    /// The Socure reference ID for challenge correlation (primary key).
    /// </summary>
    [RegularExpression(NoControlChars, ErrorMessage = ControlCharError)]
    public string? ReferenceId { get; init; }

    /// <summary>
    /// The Socure evaluation ID for challenge correlation (fallback key).
    /// </summary>
    [RegularExpression(NoControlChars, ErrorMessage = ControlCharError)]
    public string? EvalId { get; init; }

    /// <summary>
    /// The document verification decision value from Socure's data_enrichments.
    /// Expected values: "accept", "reject", or similar Socure-defined outcomes.
    /// </summary>
    [RegularExpression(NoControlChars, ErrorMessage = ControlCharError)]
    public string? DocumentDecision { get; init; }

    /// <summary>
    /// The raw webhook signature header for validation.
    /// Placeholder validation in dev; enforced in non-dev.
    /// </summary>
    public string? WebhookSignature { get; init; }
}
