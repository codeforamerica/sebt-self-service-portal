using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class SmartyAddressUpdateServiceTests
{
    private static readonly SmartySettings SmartySettings = new()
    {
        Enabled = true,
        AuthId = "test-auth-id",
        AuthToken = "test-token",
        BaseUrl = "https://us-street.api.smartystreets.com"
    };

    private static AddressValidationPolicySettings AllowGeneralDelivery { get; } = new()
    {
        AllowGeneralDelivery = true
    };

    private static AddressValidationPolicySettings DisallowGeneralDelivery { get; } = new()
    {
        AllowGeneralDelivery = false
    };

    private static SmartyAddressUpdateService CreateService(
        HttpMessageHandler handler,
        AddressValidationPolicySettings? policy = null)
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

        return new SmartyAddressUpdateService(
            factory,
            smartySnapshot,
            policySnapshot,
            NullLogger<SmartyAddressUpdateService>.Instance);
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
}

internal class HttpRequestExceptionHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Simulated network failure");
    }
}
