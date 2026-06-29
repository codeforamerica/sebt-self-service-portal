using System.Text.Json;

namespace SEBT.Portal.Infrastructure.Services.Socure;

/// <summary>
/// Parses Socure DocV fields from evaluation/webhook enrichment responses.
/// Prefers the configured <c>enrichment_provider</c>, then falls back
/// to any enrichment whose response contains <c>documentVerification</c>.
/// </summary>
public static class SocureDocumentVerificationEnrichmentParser
{
    public static JsonElement? TryGetEnrichmentResponse(
        IEnumerable<SocureEnrichmentResponseRef> enrichments,
        string docvEnrichmentProviderName)
    {
        SocureEnrichmentResponseRef? fallback = null;

        foreach (var enrichment in enrichments)
        {
            if (enrichment.Response is not { } response
                || response.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (string.Equals(
                    enrichment.EnrichmentProvider,
                    docvEnrichmentProviderName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            if (fallback == null && HasDocumentVerification(response))
            {
                fallback = enrichment;
            }
        }

        return fallback?.Response;
    }

    public static IReadOnlyList<string> ExtractReasonCodes(JsonElement enrichmentResponse)
    {
        if (!enrichmentResponse.TryGetProperty("documentVerification", out var docv))
        {
            return Array.Empty<string>();
        }

        if (!docv.TryGetProperty("reasonCodes", out var codes)
            || codes.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return codes.EnumerateArray()
            .Select(element => element.GetString())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .ToList();
    }

    public static string? ExtractDocumentDecisionValue(JsonElement enrichmentResponse)
    {
        try
        {
            return enrichmentResponse
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

    public static bool HasDocumentVerification(JsonElement response)
    {
        return response.TryGetProperty("documentVerification", out _);
    }
}

public readonly record struct SocureEnrichmentResponseRef(
    string? EnrichmentProvider,
    JsonElement? Response);
