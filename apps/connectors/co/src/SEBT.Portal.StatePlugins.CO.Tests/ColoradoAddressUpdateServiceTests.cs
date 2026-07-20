using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using HouseholdAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;

namespace SEBT.Portal.StatePlugins.CO.Tests;

[Collection("PluginCache")]
public class ColoradoAddressUpdateServiceTests : IDisposable
{
    public ColoradoAddressUpdateServiceTests() => PluginCache.ResetForTesting();
    public void Dispose() => PluginCache.ResetForTesting();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IServiceProvider BuildMinimalHostProvider()
    {
        // A minimal provider is sufficient for these tests because PluginCache.OverrideForTesting
        // is always called before construction, so PluginCache.GetOrBuild short-circuits and
        // never actually uses the provider.
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds an address-update service with a fake cache that returns <paramref name="accountDetailsJson"/>
    /// (or a default single-student response) and a fake HTTP handler for the PATCH leg.
    /// </summary>
    private static ColoradoAddressUpdateService BuildService(
        AddressUpdatePipelineMessageHandler handler,
        ICbmsHouseholdCache? fakeCache = null,
        IConfiguration? configuration = null)
    {
        fakeCache ??= BuildCacheReturning(handler.AccountDetailsJson);
        PluginCache.OverrideForTesting(fakeCache);

        return new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            configuration ?? CbmsTestConfiguration(),
            handler);
    }

    /// <summary>
    /// Returns a cache substitute whose <c>GetAsync</c> deserializes <paramref name="accountDetailsJson"/>
    /// into a <see cref="GetAccountDetailsResponse"/> and returns it.
    /// </summary>
    private static ICbmsHouseholdCache BuildCacheReturning(string accountDetailsJson)
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        var response = DeserializeAccountDetails(accountDetailsJson);
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(response);
        return fakeCache;
    }

    /// <summary>
    /// Minimal deserializer for test JSON: parses the stdntEnrollDtls array from a simple JSON
    /// object. Uses System.Text.Json for correctness.
    /// </summary>
    private static GetAccountDetailsResponse DeserializeAccountDetails(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var response = new GetAccountDetailsResponse();
        if (root.TryGetProperty("stdntEnrollDtls", out var dtlsElement) && dtlsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            response.StdntEnrollDtls = [];
            foreach (var element in dtlsElement.EnumerateArray())
            {
                var row = new GetAccountStudentDetail();
                if (element.TryGetProperty("sebtChldId", out var chldId) && chldId.TryGetInt32(out var chldIdVal))
                    row.SebtChldId = chldIdVal;
                if (element.TryGetProperty("sebtAppId", out var appId) && appId.TryGetInt32(out var appIdVal))
                    row.SebtAppId = appIdVal;
                if (element.TryGetProperty("gurdFstNm", out var fn))
                    row.GurdFstNm = fn.GetString();
                if (element.TryGetProperty("gurdLstNm", out var ln))
                    row.GurdLstNm = ln.GetString();
                if (element.TryGetProperty("gurdEmailAddr", out var email))
                    row.GurdEmailAddr = email.GetString();
                if (element.TryGetProperty("addrLn1", out var a1))
                    row.AddrLn1 = a1.GetString();
                response.StdntEnrollDtls.Add(row);
            }
        }
        else
        {
            response.StdntEnrollDtls = [];
        }

        return response;
    }

    private static IConfiguration CbmsTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "id",
                ["Cbms:ClientSecret"] = "secret",
                ["Cbms:ApiBaseUrl"] = "https://cbms-api.test/",
                ["Cbms:TokenEndpointUrl"] = "https://cbms-auth.test/token"
            })
            .Build();

    private static HouseholdAddress ValidAddress() =>
        new()
        {
            StreetAddress1 = "100 Main St",
            City = "Denver",
            State = "CO",
            PostalCode = "80202"
        };

    private static HouseholdAddress NewAddress(string line1 = "456 New St") =>
        new()
        {
            StreetAddress1 = line1,
            City = "Boulder",
            State = "CO",
            PostalCode = "80301"
        };

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("200", true)]
    [InlineData("00", true)]
    [InlineData("422", false)]
    [InlineData(null, false)]
    public void IsCbmsUpdateSuccessCode_spec_and_UAT(string? respCd, bool expected)
    {
        Assert.Equal(expected, ColoradoAddressUpdateService.IsCbmsUpdateSuccessCode(respCd));
    }

    [Theory]
    [InlineData("""{"respCd":"200","respMsg":"Success"}""")]
    [InlineData("""{"respCd":"00","respMsg":"Success"}""")]
    public async Task UpdateAddressAsync_returns_success_when_cbms_succeeds(string patchResponseJson)
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[{"sebtChldId":1,"sebtAppId":2,"gurdFstNm":"G","gurdLstNm":"H","gurdEmailAddr":"g@example.com"}]}""",
            patchResponseJson: patchResponseJson);

        var service = BuildService(handler);
        var request = new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        };

        var result = await service.UpdateAddressAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Theory]
    [InlineData("3035550199")]
    [InlineData("(303) 555-0199")]
    [InlineData("+1 303 555 0199")]
    public async Task UpdateAddressAsync_accepts_formatted_US_phones_like_case_lookup(string householdPhone)
    {
        var handler = new AddressUpdatePipelineMessageHandler();
        var service = BuildService(handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = householdPhone,
            Address = ValidAddress()
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_identifier_not_phone()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "not-a-phone",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_identifier_null()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = null!,
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_address_null()
    {
        var handler = new AddressUpdatePipelineMessageHandler();
        var service = BuildService(handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = null!
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_ADDRESS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_success_when_row_has_only_sebtAppId()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[{"sebtAppId":999}]}""");

        var service = BuildService(handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_success_when_multiple_children_sends_array_body()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """
                {"stdntEnrollDtls":[
                  {"sebtChldId":1,"sebtAppId":10},
                  {"sebtChldId":2,"sebtAppId":20}
                ]}
                """);

        var service = BuildService(handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_backend_error_when_cbms_not_configured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "",
                ["Cbms:ClientSecret"] = ""
            })
            .Build();

        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            config,
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_backend_error_when_cbms_urls_not_configured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "id",
                ["Cbms:ClientSecret"] = "secret",
                ["Cbms:ApiBaseUrl"] = "",
                ["Cbms:TokenEndpointUrl"] = ""
            })
            .Build();

        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            config,
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_no_enrollment_rows_when_response_has_empty_rows()
    {
        // Non-null response carrying an empty stdntEnrollDtls array (distinct from the cache
        // returning null). Differentiated so logs/metrics can tell this apart from a true not-found.
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[]}""");

        var service = BuildService(handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("NO_ENROLLMENT_ROWS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_household_not_found_when_cache_returns_null()
    {
        // Production negative-cache path: CbmsHouseholdCache collapses a missing account
        // (or empty rows) to a null response, which maps to HOUSEHOLD_NOT_FOUND.
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GetAccountDetailsResponse?)null);

        var service = BuildService(new AddressUpdatePipelineMessageHandler(), fakeCache);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("HOUSEHOLD_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_no_actionable_enrollment_when_rows_lack_usable_ids()
    {
        // Rows came back, but none carry a usable sebtChldId/sebtAppId, so no update payload
        // can be built. This signals a CBMS data/mapping problem, distinct from not-found.
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[{"gurdFstNm":"G","gurdLstNm":"H"}]}""");

        var service = BuildService(handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("NO_ACTIONABLE_ENROLLMENT", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_maps_cbms_400_ErrorResponse_to_http_prefixed_message()
    {
        // The cache throws ErrorResponse on CBMS 4xx; verify the error is mapped correctly.
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        var errorResponse = new ErrorResponse
        {
            ErrorDetails =
            [
                new ErrorDetail { Code = "400", Message = "Bad Request" }
            ],
            CorrelationId = "11174770-a6a1-4949-b216-622e363e872e",
            ResponseStatusCode = 400
        };
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<GetAccountDetailsResponse?>(_ => throw errorResponse);
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CBMS_400", result.ErrorCode);
        Assert.StartsWith("HTTP 400:", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Bad Request", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("correlationId:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAddressAsync_writes_through_to_cache_on_PATCH_success()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        var existingResponse = new GetAccountDetailsResponse
        {
            StdntEnrollDtls =
            [
                new GetAccountStudentDetail
                {
                    SebtChldId = 1,
                    SebtAppId = 2,
                    AddrLn1 = "100 Old St"
                }
            ]
        };
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(existingResponse);
        PluginCache.OverrideForTesting(fakeCache);

        var handler = new AddressUpdatePipelineMessageHandler(patchResponseJson: """{"respCd":"00","respMsg":"Success"}""");
        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = NewAddress("456 New St")
        });

        Assert.True(result.IsSuccess);
        await fakeCache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Is<GetAccountDetailsResponse>(r =>
                r.StdntEnrollDtls != null &&
                r.StdntEnrollDtls.Count == 1 &&
                r.StdntEnrollDtls[0].AddrLn1 == "456 New St"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddressAsync_does_not_write_through_on_PATCH_failure()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new GetAccountDetailsResponse
            {
                StdntEnrollDtls =
                [
                    new GetAccountStudentDetail { SebtChldId = 1, SebtAppId = 2 }
                ]
            });
        PluginCache.OverrideForTesting(fakeCache);

        var handler = new AddressUpdatePipelineMessageHandler(patchResponseJson: """{"respCd":"422","respMsg":"Validation error"}""");
        var service = new ColoradoAddressUpdateService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        await fakeCache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<GetAccountDetailsResponse>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Returns OAuth token JSON and a configurable PATCH update-std-dtls response.
    /// The get-account-details path is NOT handled here — those reads now come from
    /// the household cache (substituted via PluginCache.OverrideForTesting).
    /// </summary>
    private sealed class AddressUpdatePipelineMessageHandler : HttpMessageHandler
    {
        private readonly string _patchResponseJson;

        public bool ReceivedPatch { get; private set; }

        /// <summary>
        /// The account-details JSON that was passed at construction; stored so
        /// <see cref="BuildService"/> can feed the same data to the cache substitute.
        /// </summary>
        internal string AccountDetailsJson { get; }

        public AddressUpdatePipelineMessageHandler(
            string? accountDetailsJson = null,
            string? patchResponseJson = null,
            HttpStatusCode accountDetailsStatusCode = HttpStatusCode.OK)
        {
            // accountDetailsStatusCode is no longer used for HTTP interception since reads
            // come from the cache, but the parameter is kept for call-site compatibility.
            _ = accountDetailsStatusCode;
            AccountDetailsJson = accountDetailsJson
                ?? """{"stdntEnrollDtls":[{"sebtChldId":1,"sebtAppId":2}]}""";
            _patchResponseJson = patchResponseJson
                ?? """{"respCd":"200","respMsg":"Success"}""";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            if (url.Contains("token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                const string tokenJson = """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""";
                return Task.FromResult(Json(HttpStatusCode.OK, tokenJson));
            }

            if (url.Contains("update-std-dtls", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Patch)
            {
                ReceivedPatch = true;
                return Task.FromResult(Json(HttpStatusCode.OK, _patchResponseJson));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
