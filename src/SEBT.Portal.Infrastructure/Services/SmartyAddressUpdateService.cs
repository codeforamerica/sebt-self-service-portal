using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Calls Smarty US Street Address API to validate and normalize US mailing addresses.
/// </summary>
public sealed class SmartyAddressUpdateService(
    IHttpClientFactory httpClientFactory,
    IOptionsSnapshot<SmartySettings> smartySettingsSnapshot,
    IOptionsSnapshot<AddressValidationPolicySettings> policySettingsSnapshot,
    ILogger<SmartyAddressUpdateService> logger) : IAddressUpdateService
{
    private static readonly JsonSerializerOptions SmartyRequestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<Result<AddressUpdateSuccess>> ValidateAndNormalizeAsync(
        AddressUpdateOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = policySettingsSnapshot.Value;
        var settings = smartySettingsSnapshot.Value;

        var inputAddress = AddressNormalizationHelper.TrimToAddress(
            request.StreetAddress1,
            request.StreetAddress2,
            request.City,
            request.State,
            request.PostalCode);

        if (GeneralDeliveryDetection.TextIndicatesGeneralDelivery(
                inputAddress.StreetAddress1,
                inputAddress.StreetAddress2)
            && !policy.AllowGeneralDelivery)
        {
            return Result<AddressUpdateSuccess>.ValidationFailed(
                "streetAddress1",
                "General Delivery addresses are not accepted for this state.");
        }

        AddressNormalizationHelper.SplitPostalCode(inputAddress.PostalCode ?? string.Empty, out var zip5, out var plus4);

        var zipForSmarty = plus4 != null ? $"{zip5}{plus4}" : zip5;

        var payload = new[]
        {
            new SmartyStreetInput
            {
                Street = inputAddress.StreetAddress1,
                Secondary = inputAddress.StreetAddress2,
                City = inputAddress.City,
                State = inputAddress.State,
                Zipcode = zipForSmarty
            }
        };

        var json = JsonSerializer.Serialize(payload, SmartyRequestJsonOptions);
        var query = BuildAuthQuery(settings.AuthId!, settings.AuthToken!);
        var httpClient = httpClientFactory.CreateClient("Smarty");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "street-address" + query)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Smarty US Street API returned {StatusCode} for correlation {CorrelationId}",
                    (int)httpResponse.StatusCode,
                    request.CorrelationId ?? "(none)");
                return Result<AddressUpdateSuccess>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    "Address verification is temporarily unavailable. Please try again later.");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                logger.LogWarning(
                    "Smarty US Street API returned empty body for correlation {CorrelationId}",
                    request.CorrelationId ?? "(none)");
                return Result<AddressUpdateSuccess>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    "Address verification returned an unexpected response.");
            }

            List<SmartyCandidateDto>? candidates;
            try
            {
                candidates = JsonSerializer.Deserialize<List<SmartyCandidateDto>>(body, SmartyJsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Smarty response could not be parsed.");
                return Result<AddressUpdateSuccess>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    "Address verification returned an unexpected response.");
            }

            var best = SelectBestCandidate(candidates);
            if (best == null)
            {
                return Result<AddressUpdateSuccess>.ValidationFailed(
                    "address",
                    "This address could not be verified. Check the street, city, state, and ZIP code.");
            }

            var isGeneralDelivery = GeneralDeliveryDetection.IsGeneralDeliveryRecordType(best.Metadata?.RecordType)
                || GeneralDeliveryDetection.TextIndicatesGeneralDelivery(
                    best.DeliveryLine1,
                    best.DeliveryLine2);

            if (isGeneralDelivery && !policy.AllowGeneralDelivery)
            {
                return Result<AddressUpdateSuccess>.ValidationFailed(
                    "streetAddress1",
                    "General Delivery addresses are not accepted for this state.");
            }

            if (!isGeneralDelivery && !IsDeliverableByDpv(best.Analysis?.DpvMatchCode))
            {
                return Result<AddressUpdateSuccess>.ValidationFailed(
                    "address",
                    "This address could not be verified as a deliverable USPS address.");
            }

            var normalized = MapToAddress(best);
            var wasCorrected = !AddressNormalizationHelper.AddressesEqualLoose(inputAddress, normalized);

            return Result<AddressUpdateSuccess>.Success(
                new AddressUpdateSuccess
                {
                    NormalizedAddress = normalized,
                    IsGeneralDelivery = isGeneralDelivery,
                    WasCorrected = wasCorrected
                });
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Smarty request timed out.");
            return Result<AddressUpdateSuccess>.DependencyFailed(
                DependencyFailedReason.Timeout,
                "Address verification timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Smarty HTTP request failed.");
            return Result<AddressUpdateSuccess>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Address verification is temporarily unavailable. Please try again later.");
        }
    }

    private static string BuildAuthQuery(string authId, string authToken) =>
        $"?auth-id={Uri.EscapeDataString(authId)}&auth-token={Uri.EscapeDataString(authToken)}";

    private static SmartyCandidateDto? SelectBestCandidate(List<SmartyCandidateDto>? candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .Where(c => c.InputIndex == 0)
            .OrderBy(c => c.CandidateIndex)
            .FirstOrDefault()
            ?? candidates.OrderBy(c => c.CandidateIndex).First();
    }

    private static bool IsDeliverableByDpv(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        // Y = confirmed; S = confirmed ignoring secondary; D = primary confirmed, secondary issue
        return code.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || code.Equals("S", StringComparison.OrdinalIgnoreCase)
            || code.Equals("D", StringComparison.OrdinalIgnoreCase);
    }

    private static Address MapToAddress(SmartyCandidateDto c)
    {
        var zip = c.Components?.Zipcode ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(c.Components?.Plus4Code))
        {
            zip = $"{zip}-{c.Components!.Plus4Code}";
        }

        return new Address
        {
            StreetAddress1 = (c.DeliveryLine1 ?? string.Empty).Trim(),
            StreetAddress2 = string.IsNullOrWhiteSpace(c.DeliveryLine2) ? null : c.DeliveryLine2.Trim(),
            City = (c.Components?.CityName ?? string.Empty).Trim(),
            State = (c.Components?.StateAbbreviation ?? string.Empty).Trim(),
            PostalCode = AddressNormalizationHelper.FormatPostalCode(zip)
        };
    }

    private static readonly JsonSerializerOptions SmartyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SmartyStreetInput
    {
        [JsonPropertyName("street")]
        public string? Street { get; set; }

        [JsonPropertyName("secondary")]
        public string? Secondary { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("zipcode")]
        public string? Zipcode { get; set; }
    }

    private sealed class SmartyCandidateDto
    {
        [JsonPropertyName("input_index")]
        public int InputIndex { get; set; }

        [JsonPropertyName("candidate_index")]
        public int CandidateIndex { get; set; }

        [JsonPropertyName("delivery_line_1")]
        public string? DeliveryLine1 { get; set; }

        [JsonPropertyName("delivery_line_2")]
        public string? DeliveryLine2 { get; set; }

        [JsonPropertyName("components")]
        public SmartyComponentsDto? Components { get; set; }

        [JsonPropertyName("metadata")]
        public SmartyMetadataDto? Metadata { get; set; }

        [JsonPropertyName("analysis")]
        public SmartyAnalysisDto? Analysis { get; set; }
    }

    private sealed class SmartyComponentsDto
    {
        [JsonPropertyName("city_name")]
        public string? CityName { get; set; }

        [JsonPropertyName("state_abbreviation")]
        public string? StateAbbreviation { get; set; }

        [JsonPropertyName("zipcode")]
        public string? Zipcode { get; set; }

        [JsonPropertyName("plus4_code")]
        public string? Plus4Code { get; set; }
    }

    private sealed class SmartyMetadataDto
    {
        [JsonPropertyName("record_type")]
        public string? RecordType { get; set; }
    }

    private sealed class SmartyAnalysisDto
    {
        [JsonPropertyName("dpv_match_code")]
        public string? DpvMatchCode { get; set; }
    }
}
