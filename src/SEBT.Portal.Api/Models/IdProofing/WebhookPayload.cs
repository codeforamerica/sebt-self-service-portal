using System.Text.Json.Serialization;

namespace SEBT.Portal.Api.Models.IdProofing;

/// <summary>
/// Incoming Socure webhook payload. Mapped to the ProcessWebhookCommand.
/// This is a simplified representation — the full Socure payload is more complex,
/// but we only extract the fields we need for processing.
/// </summary>
public class WebhookPayload
{
    /// <summary>Socure event identifier for idempotency.</summary>
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    /// <summary>Socure reference ID for challenge correlation (primary key).</summary>
    [JsonPropertyName("reference_id")]
    public string? ReferenceId { get; set; }

    /// <summary>Socure evaluation ID for challenge correlation (fallback key).</summary>
    [JsonPropertyName("eval_id")]
    public string? EvalId { get; set; }

    /// <summary>Enrichment data containing the document verification decision.</summary>
    [JsonPropertyName("data_enrichments")]
    public DataEnrichments? DataEnrichments { get; set; }
}

/// <summary>
/// Wrapper for the data_enrichments section of the Socure webhook payload.
/// </summary>
public class DataEnrichments
{
    /// <summary>Document verification result, if present.</summary>
    [JsonPropertyName("documentVerification")]
    public DocumentVerification? DocumentVerification { get; set; }
}

/// <summary>
/// Document verification result from Socure.
/// </summary>
public class DocumentVerification
{
    /// <summary>The decision object containing the verification outcome.</summary>
    [JsonPropertyName("decision")]
    public VerificationDecision? Decision { get; set; }
}

/// <summary>
/// The verification decision from Socure's document analysis.
/// </summary>
public class VerificationDecision
{
    /// <summary>Decision value: "accept" or "reject".</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
