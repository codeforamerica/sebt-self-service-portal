using System.Text.Json;
using System.Text.Json.Serialization;

namespace SEBT.Portal.Infrastructure.Services.Socure;

// --- Outbound request ---

internal class SocureEvaluationRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("workflow")]
    public string Workflow { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public SocureEvaluationRequestData Data { get; init; } = new();
}

internal class SocureEvaluationRequestData
{
    [JsonPropertyName("individual")]
    public SocureIndividual Individual { get; init; } = new();
}

internal class SocureIndividual
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; init; }

    [JsonPropertyName("national_id")]
    public string? NationalId { get; init; }

    [JsonPropertyName("docv")]
    public SocureDocvConfig? Docv { get; init; }
}

internal class SocureDocvConfig
{
    [JsonPropertyName("config")]
    public object Config { get; init; } = new { };
}

// --- Inbound response ---

internal class SocureEvaluationResponse
{
    [JsonPropertyName("eval_id")]
    public string? EvalId { get; init; }

    [JsonPropertyName("decision")]
    public string? Decision { get; init; }

    [JsonPropertyName("eval_status")]
    public string? EvalStatus { get; init; }

    [JsonPropertyName("data_enrichments")]
    public List<SocureDataEnrichment>? DataEnrichments { get; init; }
}

internal class SocureDataEnrichment
{
    [JsonPropertyName("enrichment_name")]
    public string? EnrichmentName { get; init; }

    [JsonPropertyName("enrichment_provider")]
    public string? EnrichmentProvider { get; init; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; init; }

    // JsonElement? because each enrichment has a different response shape.
    // We parse relevant fields manually based on EnrichmentName.
    [JsonPropertyName("response")]
    public JsonElement? Response { get; init; }
}
