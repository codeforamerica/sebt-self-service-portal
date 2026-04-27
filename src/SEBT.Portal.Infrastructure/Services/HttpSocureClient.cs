using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
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
        Guid userId,
        string email,
        string dateOfBirth,
        string? idType,
        string? idValue,
        string? ipAddress = null,
        string? phoneNumber = null,
        string? givenName = null,
        string? familyName = null,
        Address? address = null,
        string? diSessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var settings = socureSettingsSnapshot.Value;

        // Normalize phone to E.164 at the Socure boundary. Defense-in-depth:
        // malformed upstream values (e.g. seed-data artifacts) must not reach the API.
        var normalizedPhone = PhoneNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null && !string.IsNullOrWhiteSpace(phoneNumber))
        {
            logger.LogWarning("Phone number could not be normalized to E.164; dropping from Socure payload");
        }
        var e164Phone = normalizedPhone is null ? null : $"+1{normalizedPhone}";

        // Truncate names to OpenAPI Individual maxLength (240). Names are CMS-sourced and
        // never user-entered, so the FE cannot police this. Truncation is preferred over
        // rejection: the name itself is valid, just over-long.
        var truncatedGivenName = TruncateNameOrWarn(givenName, nameof(givenName));
        var truncatedFamilyName = TruncateNameOrWarn(familyName, nameof(familyName));

        var request = BuildEvaluationRequest(userId, email, dateOfBirth, idType, idValue, settings, ipAddress, e164Phone, truncatedGivenName, truncatedFamilyName, address, diSessionToken);
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
        Guid userId,
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
        Guid userId,
        string email,
        string dateOfBirth,
        string? idType,
        string? idValue,
        SocureSettings settings,
        string? ipAddress = null,
        string? phoneNumber = null,
        string? givenName = null,
        string? familyName = null,
        Address? address = null,
        string? diSessionToken = null)
    {
        // Frontend-provided DI token takes precedence over config fallback
        var effectiveDiToken = !string.IsNullOrWhiteSpace(diSessionToken)
            ? diSessionToken
            : !string.IsNullOrWhiteSpace(settings.DiSessionToken)
                ? settings.DiSessionToken
                : null;

        var sendIdentifierToSocure = !string.IsNullOrWhiteSpace(idType)
            && !SocureExcludedIdentifierTypes.IsExcludedFromSocurePayload(idType);

        var mappedAddress = MapAddress(address);
        if (mappedAddress == null)
        {
            // Consumer onboarding docs require address.country at minimum for many workflows.
            mappedAddress = new SocureAddress
            {
                Type = "mailing",
                Country = "US"
            };
        }
        else if (string.IsNullOrWhiteSpace(mappedAddress.Country))
        {
            mappedAddress = new SocureAddress
            {
                Type = mappedAddress.Type ?? "mailing",
                Line1 = mappedAddress.Line1,
                Line2 = mappedAddress.Line2,
                Locality = mappedAddress.Locality,
                MajorAdminDivision = mappedAddress.MajorAdminDivision,
                PostalCode = mappedAddress.PostalCode,
                Country = "US"
            };
        }

        var individual = new SocureIndividual
        {
            CustomerIndividualId = userId.ToString(),
            Email = email,
            DateOfBirth = dateOfBirth,
            Country = "US",
            NationalId = sendIdentifierToSocure && !string.IsNullOrWhiteSpace(idValue)
                ? idValue
                : null,
            DiSessionToken = effectiveDiToken,
            IpAddress = ipAddress,
            PhoneNumber = phoneNumber,
            GivenName = givenName,
            FamilyName = familyName,
            Docv = new SocureDocvConfig(),
            Address = mappedAddress
        };

        return new SocureEvaluationRequest
        {
            // Per OpenAPI ConsumerOnboarding.id: must be unique per evaluation. Reusing an id
            // causes RiskOS to treat the request as a re-run and can impact downstream workflows.
            // The customer/transaction identifier stays on individual.id (userId).
            Id = Guid.NewGuid().ToString(),
            Workflow = settings.Workflow,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Data = new SocureEvaluationRequestData { Individual = individual }
        };
    }

    /// <summary>
    /// Truncates a name to <see cref="MaxNameLength"/> characters if needed, logging a warning.
    /// OpenAPI Individual.given_name and Individual.family_name both specify maxLength: 240.
    /// </summary>
    private string? TruncateNameOrWarn(string? name, string fieldName)
    {
        if (name is null || name.Length <= MaxNameLength)
        {
            return name;
        }

        logger.LogWarning(
            "Name field {FieldName} exceeded {MaxLength} chars ({ActualLength}); truncating for Socure payload",
            fieldName, MaxNameLength, name.Length);
        return name[..MaxNameLength];
    }

    private const int MaxNameLength = 240;

    private static SocureAddress? MapAddress(Address? address)
    {
        if (address == null)
            return null;

        return new SocureAddress
        {
            Type = "mailing",
            Line1 = address.StreetAddress1,
            Line2 = address.StreetAddress2,
            Locality = address.City,
            MajorAdminDivision = address.State,
            PostalCode = address.PostalCode,
            Country = "US"
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
            AllowIdRetry: true, // Socure doesn't provide retry guidance; handler overrides based on attempt count
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
