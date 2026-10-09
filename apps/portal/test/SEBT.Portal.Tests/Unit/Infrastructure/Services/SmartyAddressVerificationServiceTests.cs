using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class SmartyAddressVerificationServiceTests
{
    private static readonly SmartySettings SmartySettings = new()
    {
        Enabled = true,
        AuthId = "test-auth-id",
        AuthToken = "test-token",
        BaseUrl = "https://us-street.api.smarty.com"
    };

    private static AddressValidationPolicySettings AllowGeneralDelivery { get; } = new()
    {
        AllowGeneralDelivery = true
    };

    private static AddressValidationPolicySettings DisallowGeneralDelivery { get; } = new()
    {
        AllowGeneralDelivery = false
    };

    private static SmartyAddressVerificationService CreateService(
        HttpMessageHandler handler,
        AddressValidationPolicySettings? policy = null,
        ILogger<SmartyAddressVerificationService>? logger = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(SmartySettings.BaseUrl.TrimEnd('/') + "/")
        };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Smarty").Returns(httpClient);

        var smartySnapshot = Substitute.For<IOptionsSnapshot<SmartySettings>>();
        smartySnapshot.Value.Returns(SmartySettings);
        var policySnapshot = Substitute.For<IOptionsSnapshot<AddressValidationPolicySettings>>();
        policySnapshot.Value.Returns(policy ?? AllowGeneralDelivery);

        return new SmartyAddressVerificationService(
            factory,
            smartySnapshot,
            policySnapshot,
            logger ?? NullLogger<SmartyAddressVerificationService>.Instance);
    }

    private static AddressUpdateOperationRequest BaseRequest() =>
        new()
        {
            StreetAddress1 = "123 Main St",
            City = "Washington",
            State = "DC",
            PostalCode = "20001"
        };

    [Fact]
    public async Task ValidateAndNormalizeAsync_ReturnsNormalizedAddress_WhenSmartyReturnsMatch()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "123 Main St",
              "delivery_line_2": null,
              "components": {
                "primary_number": "123",
                "street_name": "Main",
                "street_suffix": "St",
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": "1234"
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.Equal("123 Main St", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Equal("DC", success.Value.NormalizedAddress.State);
        Assert.Equal("20001-1234", success.Value.NormalizedAddress.PostalCode);
        Assert.False(success.Value.IsGeneralDelivery);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_SetsWasCorrectedTrue_WhenSmartyNormalizesAddress()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "123 Main St NW",
              "delivery_line_2": null,
              "components": {
                "primary_number": "123",
                "street_name": "Main",
                "street_suffix": "St",
                "street_postdirection": "NW",
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": "1234"
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var request = new AddressUpdateOperationRequest
        {
            StreetAddress1 = "123 main st nw",
            City = "washington",
            State = "DC",
            PostalCode = "20001"
        };

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(request);

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.True(success.Value.WasCorrected);
        Assert.Equal("123 Main St NW", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Equal("20001-1234", success.Value.NormalizedAddress.PostalCode);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_ReturnsValidationFailed_WhenSmartyReturnsNoCandidates()
    {
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, "[]"));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        Assert.IsType<ValidationFailedResult<AddressUpdateSuccess>>(result);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_ReturnsValidationFailed_WhenDpvIsNotDeliverable()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "999 Fake St",
              "components": {
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": null
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "N" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        Assert.IsType<ValidationFailedResult<AddressUpdateSuccess>>(result);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_RejectsGeneralDelivery_WhenPolicyDisallows()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "General Delivery",
              "components": {
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": null
              },
              "metadata": { "record_type": "G" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json), DisallowGeneralDelivery);
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var vf = Assert.IsType<ValidationFailedResult<AddressUpdateSuccess>>(result);
        Assert.Contains(vf.Errors, e => e.Key == "streetAddress1");
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_AcceptsGeneralDelivery_WhenPolicyAllows()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "General Delivery",
              "components": {
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": null
              },
              "metadata": { "record_type": "G" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json), AllowGeneralDelivery);
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.True(success.Value.IsGeneralDelivery);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_RejectsGeneralDeliveryOnInput_WhenPolicyDisallows()
    {
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, "[]"), DisallowGeneralDelivery);
        var request = BaseRequest() with { StreetAddress1 = "General Delivery" };

        var result = await service.ValidateAndNormalizeAsync(request);

        var vf = Assert.IsType<ValidationFailedResult<AddressUpdateSuccess>>(result);
        Assert.Contains(vf.Errors, e => e.Key == "streetAddress1");
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_ReturnsDependencyFailed_WhenSmartyReturnsNon2xx()
    {
        var service = CreateService(new MockHttpHandler(HttpStatusCode.InternalServerError, "{}"));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var df = Assert.IsType<DependencyFailedResult<AddressUpdateSuccess>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, df.Reason);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_ReturnsDependencyFailed_OnTimeout()
    {
        var service = CreateService(new TimeoutHandler());
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var df = Assert.IsType<DependencyFailedResult<AddressUpdateSuccess>>(result);
        Assert.Equal(DependencyFailedReason.Timeout, df.Reason);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_ReturnsDependencyFailed_OnHttpRequestException()
    {
        var service = CreateService(new HttpRequestExceptionHandler());
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var df = Assert.IsType<DependencyFailedResult<AddressUpdateSuccess>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, df.Reason);
    }

    // Smarty's delivery_line_1 is a USPS-style mailing label (primary + street + secondary
    // concatenated). The components object exposes the structured parts. We must build
    // StreetAddress1 and StreetAddress2 from components so the secondary unit lands on
    // line 2 instead of being collapsed into line 1.
    [Fact]
    public async Task ValidateAndNormalizeAsync_BuildsLine1FromComponents_WhenPredirectionIsPresent()
    {
        // Honolulu-style address with a predirection (e.g. "123 N King St").
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "123 N King St",
              "delivery_line_2": null,
              "components": {
                "primary_number": "123",
                "street_predirection": "N",
                "street_name": "King",
                "street_suffix": "St",
                "city_name": "Honolulu",
                "state_abbreviation": "HI",
                "zipcode": "96813",
                "plus4_code": null
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.Equal("123 N King St", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Null(success.Value.NormalizedAddress.StreetAddress2);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_BuildsLine2FromPmbComponents_WhenSecondaryIsAbsent()
    {
        // Private mailbox (PMB) only, no apartment-style secondary.
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "456 Oak Ave PMB 12",
              "delivery_line_2": null,
              "components": {
                "primary_number": "456",
                "street_name": "Oak",
                "street_suffix": "Ave",
                "pmb_designator": "PMB",
                "pmb_number": "12",
                "city_name": "Denver",
                "state_abbreviation": "CO",
                "zipcode": "80202",
                "plus4_code": null
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.Equal("456 Oak Ave", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Equal("PMB 12", success.Value.NormalizedAddress.StreetAddress2);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_PreservesDeliveryLine2_WhenSmartyPopulatesIt()
    {
        // delivery_line_2 is rare but used for "C/O" and similar lines. Append after any
        // structured secondary so we don't drop the carrier-routed text.
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "789 Pine Rd",
              "delivery_line_2": "C/O Jane Doe",
              "components": {
                "primary_number": "789",
                "street_name": "Pine",
                "street_suffix": "Rd",
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": null
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(BaseRequest());

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.Equal("789 Pine Rd", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Equal("C/O Jane Doe", success.Value.NormalizedAddress.StreetAddress2);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_SplitsSecondaryUnitFromLine1_UsingComponents()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "123 Main St NW Apt 5",
              "delivery_line_2": null,
              "components": {
                "primary_number": "123",
                "street_name": "Main",
                "street_suffix": "St",
                "street_postdirection": "NW",
                "secondary_designator": "Apt",
                "secondary_number": "5",
                "city_name": "Washington",
                "state_abbreviation": "DC",
                "zipcode": "20001",
                "plus4_code": "1234"
              },
              "metadata": { "record_type": "H" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var request = BaseRequest() with { StreetAddress1 = "123 Main St NW", StreetAddress2 = "Apt 5" };
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, json));
        var result = await service.ValidateAndNormalizeAsync(request);

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.Equal("123 Main St NW", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Equal("Apt 5", success.Value.NormalizedAddress.StreetAddress2);
    }
    private const string VerifiedJson =
        """
        [{
          "input_index": 0,
          "candidate_index": 0,
          "delivery_line_1": "123 Main St",
          "delivery_line_2": null,
          "components": {
            "primary_number": "123",
            "street_name": "Main",
            "street_suffix": "St",
            "city_name": "Washington",
            "state_abbreviation": "DC",
            "zipcode": "20001",
            "plus4_code": null
          },
          "metadata": { "record_type": "S" },
          "analysis": { "dpv_match_code": "Y" }
        }]
        """;

    private const string NotDeliverableJson =
        """
        [{
          "input_index": 0,
          "candidate_index": 0,
          "delivery_line_1": "999 Fake St",
          "components": {
            "city_name": "Washington",
            "state_abbreviation": "DC",
            "zipcode": "20001",
            "plus4_code": null
          },
          "metadata": { "record_type": "S" },
          "analysis": { "dpv_match_code": "N" }
        }]
        """;

    private const string GeneralDeliveryJson =
        """
        [{
          "input_index": 0,
          "candidate_index": 0,
          "delivery_line_1": "General Delivery",
          "components": {
            "city_name": "Washington",
            "state_abbreviation": "DC",
            "zipcode": "20001",
            "plus4_code": null
          },
          "metadata": { "record_type": "G" },
          "analysis": { "dpv_match_code": "Y" }
        }]
        """;

    private static readonly string[] LookupOutcomes = new[]
    {
        "Verified", "GeneralDeliveryAccepted", "NoCandidates", "NotDeliverable", "GeneralDeliveryRejected",
        "HttpError", "EmptyBody", "ParseError", "Timeout", "TransportFailure", "Cancelled", "Faulted"
    };

    private static readonly string[] FixtureTokens =
        new[] { "123", "Main", "Washington", "DC", "20001", "999", "Fake", "General Delivery" };

    private static CapturingLogger<SmartyAddressVerificationService> NewLogger() => new();

    private static CapturedLogEntry AssertSingleLookupEvent(
        CapturingLogger<SmartyAddressVerificationService> logger,
        LogLevel expectedLevel,
        string expectedOutcome,
        params string[] expectedPropertyNames)
    {
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(expectedLevel, entry.Level);
        Assert.Null(entry.Exception);

        Assert.True(entry.Properties.TryGetValue("Outcome", out var outcomeValue), "Outcome property missing");
        var outcome = Assert.IsType<string>(outcomeValue);
        Assert.Equal(expectedOutcome, outcome);
        Assert.Contains(outcome, LookupOutcomes);

        var populated = entry.Properties
            .Where(p => p.Value is not null)
            .ToDictionary(p => p.Key, p => p.Value!);
        Assert.Equal(expectedPropertyNames.Order(), populated.Keys.Order());

        foreach (var (name, value) in populated)
        {
            Assert.True(value is string or int or long or bool, $"{name} is a {value.GetType().Name}");
        }

        AssertVocabulary(populated, "DpvMatchCode", "Y", "S", "D", "N");
        AssertVocabulary(populated, "RecordType", "S", "H", "F", "G", "P", "R");
        AssertVocabulary(
            populated,
            "ExceptionType",
            "JsonException", "TaskCanceledException", "HttpRequestException", "InvalidOperationException");
        AssertVocabulary(populated, "HttpRequestError", Enum.GetNames<HttpRequestError>());

        foreach (var value in populated.Values.OfType<string>())
        {
            foreach (var token in FixtureTokens)
            {
                Assert.DoesNotContain(token, value, StringComparison.OrdinalIgnoreCase);
            }
        }

        return entry;
    }

    private static void AssertVocabulary(
        IReadOnlyDictionary<string, object> properties,
        string name,
        params string[] allowed)
    {
        if (properties.TryGetValue(name, out var value))
        {
            Assert.Contains(Assert.IsType<string>(value), allowed);
        }
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsVerified_WhenCandidateIsDeliverable()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, VerifiedJson), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger,
            LogLevel.Information,
            "Verified",
            "Outcome", "StatusCode", "CandidateCount", "DpvMatchCode", "RecordType",
            "WasCorrected", "IsGeneralDelivery", "DurationMs");
        Assert.Equal(200, Assert.IsType<int>(entry.Properties["StatusCode"]));
        Assert.Equal(1, Assert.IsType<int>(entry.Properties["CandidateCount"]));
        Assert.Equal("Y", Assert.IsType<string>(entry.Properties["DpvMatchCode"]));
        Assert.Equal("S", Assert.IsType<string>(entry.Properties["RecordType"]));
        Assert.False(Assert.IsType<bool>(entry.Properties["WasCorrected"]));
        Assert.False(Assert.IsType<bool>(entry.Properties["IsGeneralDelivery"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsGeneralDeliveryAccepted_WhenPolicyAllows()
    {
        var logger = NewLogger();
        var service = CreateService(
            new MockHttpHandler(HttpStatusCode.OK, GeneralDeliveryJson), AllowGeneralDelivery, logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger,
            LogLevel.Information,
            "GeneralDeliveryAccepted",
            "Outcome", "StatusCode", "CandidateCount", "DpvMatchCode", "RecordType",
            "WasCorrected", "IsGeneralDelivery", "DurationMs");
        Assert.Equal("G", Assert.IsType<string>(entry.Properties["RecordType"]));
        Assert.True(Assert.IsType<bool>(entry.Properties["IsGeneralDelivery"]));
        Assert.True(Assert.IsType<bool>(entry.Properties["WasCorrected"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsNoCandidates_WhenSmartyReturnsEmptyList()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, "[]"), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger,
            LogLevel.Information,
            "NoCandidates",
            "Outcome", "StatusCode", "CandidateCount", "DurationMs");
        Assert.Equal(0, Assert.IsType<int>(entry.Properties["CandidateCount"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsNotDeliverable_WhenDpvRejectsCandidate()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, NotDeliverableJson), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger,
            LogLevel.Information,
            "NotDeliverable",
            "Outcome", "StatusCode", "CandidateCount", "DpvMatchCode", "RecordType",
            "IsGeneralDelivery", "DurationMs");
        Assert.Equal("N", Assert.IsType<string>(entry.Properties["DpvMatchCode"]));
        Assert.False(Assert.IsType<bool>(entry.Properties["IsGeneralDelivery"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsGeneralDeliveryRejected_WhenPolicyDisallows()
    {
        var logger = NewLogger();
        var service = CreateService(
            new MockHttpHandler(HttpStatusCode.OK, GeneralDeliveryJson), DisallowGeneralDelivery, logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger,
            LogLevel.Information,
            "GeneralDeliveryRejected",
            "Outcome", "StatusCode", "CandidateCount", "DpvMatchCode", "RecordType",
            "IsGeneralDelivery", "DurationMs");
        Assert.True(Assert.IsType<bool>(entry.Properties["IsGeneralDelivery"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsHttpError_WhenSmartyReturnsNon2xx()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.InternalServerError, "{}"), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger, LogLevel.Error, "HttpError", "Outcome", "StatusCode", "DurationMs");
        Assert.Equal(500, Assert.IsType<int>(entry.Properties["StatusCode"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsEmptyBody_WhenSmartyReturnsNoContent()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, ""), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger, LogLevel.Error, "EmptyBody", "Outcome", "StatusCode", "DurationMs");
        Assert.Equal(200, Assert.IsType<int>(entry.Properties["StatusCode"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsParseError_WhenBodyIsNotJson()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, "not json"), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger, LogLevel.Error, "ParseError", "Outcome", "StatusCode", "ExceptionType", "DurationMs");
        Assert.Equal("JsonException", Assert.IsType<string>(entry.Properties["ExceptionType"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsTimeout_WhenRequestTimesOut()
    {
        var logger = NewLogger();
        var service = CreateService(new TimeoutHandler(), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger, LogLevel.Error, "Timeout", "Outcome", "ExceptionType", "DurationMs");
        Assert.Equal("TaskCanceledException", Assert.IsType<string>(entry.Properties["ExceptionType"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsTransportFailure_WhenConnectionFails()
    {
        var logger = NewLogger();
        var transportFailure = new HttpRequestException(HttpRequestError.ConnectionError, "Simulated connection failure");
        var service = CreateService(new ThrowingHandler(transportFailure), logger: logger);

        await service.ValidateAndNormalizeAsync(BaseRequest());

        var entry = AssertSingleLookupEvent(
            logger, LogLevel.Error, "TransportFailure", "Outcome", "ExceptionType", "HttpRequestError", "DurationMs");
        Assert.Equal("HttpRequestException", Assert.IsType<string>(entry.Properties["ExceptionType"]));
        Assert.Equal("ConnectionError", Assert.IsType<string>(entry.Properties["HttpRequestError"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsCancelled_WhenCallerCancels()
    {
        var logger = NewLogger();
        using var cancellation = new CancellationTokenSource();
        var service = CreateService(new CancellingHandler(cancellation), logger: logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ValidateAndNormalizeAsync(BaseRequest(), cancellation.Token));

        AssertSingleLookupEvent(logger, LogLevel.Information, "Cancelled", "Outcome", "DurationMs");
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsFaulted_WhenHandlerThrowsUnexpectedly()
    {
        var logger = NewLogger();
        var service = CreateService(
            new ThrowingHandler(new InvalidOperationException("Simulated handler fault")), logger: logger);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ValidateAndNormalizeAsync(BaseRequest()));

        var entry = AssertSingleLookupEvent(
            logger, LogLevel.Error, "Faulted", "Outcome", "ExceptionType", "DurationMs");
        Assert.Equal("InvalidOperationException", Assert.IsType<string>(entry.Properties["ExceptionType"]));
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_LogsNothing_WhenGeneralDeliveryIsRejectedBeforeTheRequest()
    {
        var logger = NewLogger();
        var service = CreateService(new MockHttpHandler(HttpStatusCode.OK, "[]"), DisallowGeneralDelivery, logger);

        await service.ValidateAndNormalizeAsync(BaseRequest() with { StreetAddress1 = "General Delivery" });

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ValidateAndNormalizeAsync_UsesOneMessageTemplate_AcrossOutcomes()
    {
        var logger = NewLogger();
        var request = BaseRequest();

        await CreateService(new MockHttpHandler(HttpStatusCode.OK, VerifiedJson), logger: logger)
            .ValidateAndNormalizeAsync(request);
        await CreateService(new MockHttpHandler(HttpStatusCode.OK, "[]"), logger: logger)
            .ValidateAndNormalizeAsync(request);
        await CreateService(new MockHttpHandler(HttpStatusCode.InternalServerError, "{}"), logger: logger)
            .ValidateAndNormalizeAsync(request);
        await CreateService(new TimeoutHandler(), logger: logger)
            .ValidateAndNormalizeAsync(request);

        Assert.Equal(4, logger.Entries.Count);
        var template = Assert.Single(logger.Entries.Select(e => e.OriginalFormat).Distinct());
        Assert.False(string.IsNullOrWhiteSpace(template));
    }

}

internal class HttpRequestExceptionHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Simulated network failure");
    }
}

internal class ThrowingHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw exception;
    }
}

internal class CancellingHandler(CancellationTokenSource cancellationTokenSource) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationTokenSource.Cancel();
        throw new TaskCanceledException("Simulated caller cancellation", null, cancellationTokenSource.Token);
    }
}
