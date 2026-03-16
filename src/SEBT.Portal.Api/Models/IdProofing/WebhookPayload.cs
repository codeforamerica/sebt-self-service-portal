using System.Text.Json;
using System.Text.Json.Serialization;

namespace SEBT.Portal.Api.Models.IdProofing;

/// <summary>
/// Incoming Socure webhook payload for evaluation events.
/// Matches the real Socure webhook structure from the OpenAPI spec.
/// </summary>
public class WebhookPayload
{
    /// <summary>Socure event identifier for idempotency.</summary>
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    /// <summary>Timestamp of the event.</summary>
    [JsonPropertyName("event_at")]
    public string? EventAt { get; set; }

    /// <summary>Event type (e.g., "evaluation_completed").</summary>
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    /// <summary>Nested event data containing evaluation results and enrichments.</summary>
    [JsonPropertyName("data")]
    public WebhookData? Data { get; set; }
}

/// <summary>
/// Nested data object within the Socure webhook payload.
/// Contains the evaluation result and data enrichments.
/// </summary>
public class WebhookData
{
    /// <summary>Socure evaluation ID for challenge correlation (fallback key).</summary>
    [JsonPropertyName("eval_id")]
    public string? EvalId { get; set; }

    /// <summary>Top-level decision from the evaluation (e.g., "accept", "reject", "review").</summary>
    [JsonPropertyName("decision")]
    public string? Decision { get; set; }

    /// <summary>Array of enrichment results from the evaluation pipeline.</summary>
    [JsonPropertyName("data_enrichments")]
    public List<WebhookDataEnrichment>? DataEnrichments { get; set; }
}

/// <summary>
/// A single enrichment result from the Socure evaluation pipeline.
/// Each enrichment has a different response shape — parsed via <see cref="JsonElement"/>.
/// </summary>
public class WebhookDataEnrichment
{
    /// <summary>Name of the enrichment (e.g., "SocureDocRequest").</summary>
    [JsonPropertyName("enrichment_name")]
    public string? EnrichmentName { get; set; }

    /// <summary>Provider of the enrichment (e.g., "SocureDocRequest", "Socure").</summary>
    [JsonPropertyName("enrichment_provider")]
    public string? EnrichmentProvider { get; set; }

    /// <summary>HTTP status code from the enrichment provider.</summary>
    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    /// <summary>Enrichment-specific response data. Shape varies by provider.</summary>
    [JsonPropertyName("response")]
    public JsonElement? Response { get; set; }
}
