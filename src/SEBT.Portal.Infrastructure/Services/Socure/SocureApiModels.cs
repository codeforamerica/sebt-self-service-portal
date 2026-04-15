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

    [JsonPropertyName("di_session_token")]
    public string? DiSessionToken { get; init; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; init; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; init; }

    [JsonPropertyName("docv")]
    public SocureDocvConfig? Docv { get; init; }

    [JsonPropertyName("address")]
    public SocureAddress? Address { get; init; }
}

internal class SocureAddress
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("line_1")]
    public string? Line1 { get; init; }

    [JsonPropertyName("line_2")]
    public string? Line2 { get; init; }

    [JsonPropertyName("locality")]
    public string? Locality { get; init; }

    [JsonPropertyName("major_admin_division")]
    public string? MajorAdminDivision { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; init; }
}

internal class SocureDocvConfig
{
    [JsonPropertyName("config")]
    public SocureDocvConfigDetails Config { get; init; } = new();
}

internal class SocureDocvConfigDetails
{
    [JsonPropertyName("send_message")]
    public bool SendMessage { get; init; } = true;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";
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
