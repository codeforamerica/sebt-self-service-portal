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

    // Smarty's delivery_line_1 is a USPS mailing-label line: it concatenates the primary
    // street parts AND the secondary unit (e.g. "123 MAIN ST APT 5"). Mapping it directly
    // to StreetAddress1 collapses the apartment/suite into line 1 and loses line 2.
    // We rebuild StreetAddress1 and StreetAddress2 from the structured `components` block
    // so the secondary lands on its own line, matching what backends like CBMS expect.
    // See https://www.smarty.com/docs/delivery-line-one for Smarty's own construction recipe.
    private static Address MapToAddress(SmartyCandidateDto c)
    {
        var components = c.Components;
        var zip = components?.Zipcode ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(components?.Plus4Code))
        {
            zip = $"{zip}-{components!.Plus4Code}";
        }

        var streetAddress1 = BuildStreetAddress1(components);
        var streetAddress2 = BuildStreetAddress2(components, c.DeliveryLine2);

        // Fallback: if the response somehow lacks any street components, fall back to
        // delivery_line_1 so we degrade gracefully instead of returning an empty line.
        if (string.IsNullOrWhiteSpace(streetAddress1))
        {
            streetAddress1 = (c.DeliveryLine1 ?? string.Empty).Trim();
        }

        return new Address
        {
            StreetAddress1 = streetAddress1,
            StreetAddress2 = string.IsNullOrWhiteSpace(streetAddress2) ? null : streetAddress2,
            City = (components?.CityName ?? string.Empty).Trim(),
            State = (components?.StateAbbreviation ?? string.Empty).Trim(),
            PostalCode = AddressNormalizationHelper.FormatPostalCode(zip)
        };
    }

    private static string BuildStreetAddress1(SmartyComponentsDto? components)
    {
        if (components is null)
        {
            return string.Empty;
        }

        return JoinNonEmpty(
            components.Urbanization,
            components.PrimaryNumber,
            components.StreetPredirection,
            components.StreetName,
            components.StreetSuffix,
            components.StreetPostdirection);
    }

    private static string BuildStreetAddress2(SmartyComponentsDto? components, string? deliveryLine2)
    {
        // Order mirrors Smarty's own delivery-line-one recipe: secondary, then extra
        // secondary, then PMB. delivery_line_2 (rare — used for "C/O" lines, etc.) is
        // appended last so we don't drop information when Smarty populates it.
        var line2 = JoinNonEmpty(
            components?.SecondaryDesignator,
            components?.SecondaryNumber,
            components?.ExtraSecondaryDesignator,
            components?.ExtraSecondaryNumber,
            components?.PmbDesignator,
            components?.PmbNumber);

        var trimmedDeliveryLine2 = deliveryLine2?.Trim();
        if (!string.IsNullOrEmpty(trimmedDeliveryLine2))
        {
            line2 = string.IsNullOrEmpty(line2)
                ? trimmedDeliveryLine2
                : $"{line2} {trimmedDeliveryLine2}";
        }

        return line2;
    }

    private static string JoinNonEmpty(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

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
        [JsonPropertyName("urbanization")]
        public string? Urbanization { get; set; }

        [JsonPropertyName("primary_number")]
        public string? PrimaryNumber { get; set; }

        [JsonPropertyName("street_predirection")]
        public string? StreetPredirection { get; set; }

        [JsonPropertyName("street_name")]
        public string? StreetName { get; set; }

        [JsonPropertyName("street_suffix")]
        public string? StreetSuffix { get; set; }

        [JsonPropertyName("street_postdirection")]
        public string? StreetPostdirection { get; set; }

        [JsonPropertyName("secondary_designator")]
        public string? SecondaryDesignator { get; set; }

        [JsonPropertyName("secondary_number")]
        public string? SecondaryNumber { get; set; }

        [JsonPropertyName("extra_secondary_designator")]
        public string? ExtraSecondaryDesignator { get; set; }

        [JsonPropertyName("extra_secondary_number")]
        public string? ExtraSecondaryNumber { get; set; }

        [JsonPropertyName("pmb_designator")]
        public string? PmbDesignator { get; set; }

        [JsonPropertyName("pmb_number")]
        public string? PmbNumber { get; set; }

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
