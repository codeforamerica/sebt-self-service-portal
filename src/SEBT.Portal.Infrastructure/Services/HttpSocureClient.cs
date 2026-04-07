using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services.Socure;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Real HTTP implementation of <see cref="ISocureClient"/>.
/// Calls Socure's POST /api/evaluation endpoint for both KYC assessment and DocV token generation.
/// The Socure API returns both in a single response — see design doc for rationale.
/// </summary>
public class HttpSocureClient(
    IHttpClientFactory httpClientFactory,
    IOptionsSnapshot<SocureSettings> socureSettingsSnapshot,
    ILogger<HttpSocureClient> logger) : ISocureClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<Result<IdProofingAssessmentResult>> RunIdProofingAssessmentAsync(
        int userId,
        string email,
        string dateOfBirth,
        string? idType,
        string? idValue,
        CancellationToken cancellationToken = default)
    {
        var settings = socureSettingsSnapshot.Value;

        var request = BuildEvaluationRequest(userId, email, dateOfBirth, idType, idValue, settings);
        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);

        var httpClient = httpClientFactory.CreateClient("Socure");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}/api/evaluation")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Headers.Add("X-API-Version", settings.ApiVersion);

        try
        {
            var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Socure API returned {StatusCode} for user {UserId}",
                    httpResponse.StatusCode, userId);
                return Result<IdProofingAssessmentResult>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    $"Socure API returned {(int)httpResponse.StatusCode}.");
            }

            var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var response = JsonSerializer.Deserialize<SocureEvaluationResponse>(responseBody, JsonOptions);

            if (response == null)
            {
                logger.LogWarning("Socure API returned null/unparseable response for user {UserId}", userId);
                return Result<IdProofingAssessmentResult>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed, "Socure API returned an unparseable response.");
            }

            return Result<IdProofingAssessmentResult>.Success(MapToAssessmentResult(response, settings));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Socure API request timed out for user {UserId}", userId);
            return Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.Timeout, "Socure API request timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Socure API request failed for user {UserId}", userId);
            return Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure API connection failed.");
        }
    }

    public Task<Result<SocureDocvSession>> StartDocvSessionAsync(
        int userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        // DocV tokens are generated during RunIdProofingAssessmentAsync.
        // The Socure evaluation endpoint requires full PII (DOB, national_id) which this method doesn't have.
        // See design doc: docs/plans/2026-03-08-http-socure-client-design.md
        throw new NotSupportedException(
            "DocV tokens are generated during the evaluation call (RunIdProofingAssessmentAsync). " +
            "Use the DocvSession from the IdProofingAssessmentResult instead.");
    }

    private static SocureEvaluationRequest BuildEvaluationRequest(
        int userId,
        string email,
        string dateOfBirth,
        string? idType,
        string? idValue,
        SocureSettings settings)
    {
        var individual = new SocureIndividual
        {
            Email = email,
            DateOfBirth = dateOfBirth,
            NationalId = !string.IsNullOrWhiteSpace(idType) && !string.IsNullOrWhiteSpace(idValue)
                ? idValue
                : null,
            Docv = new SocureDocvConfig()
        };

        return new SocureEvaluationRequest
        {
            Id = userId.ToString(),
            Workflow = settings.Workflow,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Data = new SocureEvaluationRequestData { Individual = individual }
        };
    }

    private static IdProofingAssessmentResult MapToAssessmentResult(
        SocureEvaluationResponse response,
        SocureSettings settings)
    {
        var outcome = MapDecisionToOutcome(response.Decision, response.EvalStatus);
        var docvSession = ExtractDocvSession(response, settings.DocvEnrichmentName);

        return new IdProofingAssessmentResult(
            Outcome: outcome,
            AllowIdRetry: true, // Stubbed — real logic pending compliance policy
            DocvSession: docvSession);
    }

    private static IdProofingOutcome MapDecisionToOutcome(string? decision, string? evalStatus)
    {
        // "evaluation_paused" with REVIEW means DocV is pending — the user needs to upload documents
        if (string.Equals(evalStatus, "evaluation_paused", StringComparison.OrdinalIgnoreCase))
        {
            return IdProofingOutcome.DocumentVerificationRequired;
        }

        return decision?.ToUpperInvariant() switch
        {
            "ACCEPT" => IdProofingOutcome.Matched,
            "REJECT" => IdProofingOutcome.Failed,
            "REVIEW" => IdProofingOutcome.DocumentVerificationRequired,
            _ => IdProofingOutcome.Failed
        };
    }

    private static SocureDocvSession? ExtractDocvSession(
        SocureEvaluationResponse response,
        string docvEnrichmentName)
    {
        if (response.DataEnrichments == null)
            return null;

        var docRequestEnrichment = response.DataEnrichments
            .FirstOrDefault(e => string.Equals(
                e.EnrichmentProvider, docvEnrichmentName, StringComparison.OrdinalIgnoreCase));

        if (docRequestEnrichment?.Response == null)
            return null;

        try
        {
            var responseElement = docRequestEnrichment.Response.Value;

            var dataElement = responseElement.GetProperty("data");
            var token = dataElement.GetProperty("docvTransactionToken").GetString();
            var url = dataElement.GetProperty("url").GetString();
            var referenceId = responseElement.GetProperty("referenceId").GetString();

            if (token == null || url == null || referenceId == null)
                return null;

            return new SocureDocvSession(
                DocvTransactionToken: token,
                DocvUrl: url,
                ReferenceId: referenceId,
                EvalId: response.EvalId ?? string.Empty);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
