using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

[Collection("PluginCache")]
public class ColoradoCardReplacementServiceTests : IDisposable
{
    public ColoradoCardReplacementServiceTests() => PluginCache.ResetForTesting();
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
    /// Builds a card-replacement service with a fake cache that returns the account details
    /// produced by <paramref name="accountDetailsJson"/> and a fake HTTP handler for the PATCH leg.
    /// </summary>
    private static ColoradoCardReplacementService BuildService(
        CardReplacementPipelineMessageHandler handler,
        ICbmsHouseholdCache? fakeCache = null,
        IConfiguration? configuration = null)
    {
        fakeCache ??= BuildCacheReturning(handler.AccountDetailsJson);
        PluginCache.OverrideForTesting(fakeCache);

        return new ColoradoCardReplacementService(
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
    /// Builds an <see cref="ICbmsHouseholdCache"/> substitute that returns a
    /// <see cref="GetAccountDetailsResponse"/> containing a single student row whose
    /// <c>sebtChldCwin</c> matches <paramref name="cwin"/>, with <c>sebtChldId=1</c> and <c>sebtAppId=2</c>.
    /// </summary>
    private static ICbmsHouseholdCache BuildCacheWithMatchingCwin(string cwin)
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(BuildAccountDetailsWithMatchingCwin(cwin));
        return fakeCache;
    }

    private static GetAccountDetailsResponse BuildAccountDetailsWithMatchingCwin(string cwin)
    {
        var row = new GetAccountStudentDetail
        {
            SebtChldId = 1,
            SebtAppId = 2
        };
        // SebtChldCwin is int? in the generated model.
        if (int.TryParse(cwin, out var cwinInt))
            row.SebtChldCwin = cwinInt;

        return new GetAccountDetailsResponse
        {
            StdntEnrollDtls = [row]
        };
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
                if (element.TryGetProperty("sebtChldCwin", out var cwin) && cwin.TryGetInt32(out var cwinVal))
                    row.SebtChldCwin = cwinVal;
                if (element.TryGetProperty("sebtChldId", out var chldId) && chldId.TryGetInt32(out var chldIdVal))
                    row.SebtChldId = chldIdVal;
                if (element.TryGetProperty("sebtAppId", out var appId) && appId.TryGetInt32(out var appIdVal))
                    row.SebtAppId = appIdVal;
                if (element.TryGetProperty("stdntEligSts", out var eligSts) && eligSts.ValueKind == System.Text.Json.JsonValueKind.String)
                    row.StdntEligSts = eligSts.GetString();
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

    private static string SingleCwinEnrollmentJson(string cwin, int chldId, int appId) =>
        $$"""{"stdntEnrollDtls":[{"sebtChldCwin":{{cwin}},"sebtChldId":{{chldId}},"sebtAppId":{{appId}}}]}""";

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("""{"respCd":"200","respMsg":"Success"}""")]
    [InlineData("""{"respCd":"00","respMsg":"Success"}""")]
    public async Task RequestCardReplacementAsync_returns_success_when_cbms_succeeds(string patchResponseJson)
    {
        var handler = new CardReplacementPipelineMessageHandler(
            patchResponseJson: patchResponseJson,
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Theory]
    [InlineData("3035550199")]
    [InlineData("(303) 555-0199")]
    [InlineData("+1 303 555 0199")]
    public async Task RequestCardReplacementAsync_accepts_formatted_US_phones_like_case_lookup(string householdPhone)
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = householdPhone,
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_identifier_not_phone()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "not-a-phone",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_identifier_null()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = null!,
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_case_ids_empty()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_CASE_IDS", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_backend_error_when_cbms_not_configured()
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

        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            config,
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_no_enrollment_rows()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[]}""");

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("HOUSEHOLD_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_case_ids_do_not_match_any_row()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "9999999" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CASES_NOT_FOUND", result.ErrorCode);
        Assert.False(handler.ReceivedPatch);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_fails_when_some_case_ids_do_not_match()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }, new CaseRef { SummerEbtCaseId = "9999999" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CASES_NOT_FOUND", result.ErrorCode);
        Assert.False(handler.ReceivedPatch);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_sends_multi_body_array_when_multiple_cases_match()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: """
                {"stdntEnrollDtls":[
                  {"sebtChldCwin":1001,"sebtChldId":1,"sebtAppId":10},
                  {"sebtChldCwin":1002,"sebtChldId":2,"sebtAppId":20}
                ]}
                """);

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "1001" }, new CaseRef { SummerEbtCaseId = "1002" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
        Assert.Equal(2, handler.LastPatchBodyElementCount);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_sets_reqNewCard_Y_on_each_body()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = BuildService(handler);

        await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(handler.ReceivedPatch);
        Assert.Contains("\"reqNewCard\":\"Y\"", handler.LastPatchBodyRaw);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_backend_error_when_cbms_returns_unexpected_respCd()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2),
            patchResponseJson: """{"respCd":"422","respMsg":"Unprocessable"}""");

        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CBMS_UPDATE_FAILED", result.ErrorCode);
        Assert.Contains("Unprocessable", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_maps_cbms_400_ErrorResponse_to_http_prefixed_message()
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

        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CBMS_400", result.ErrorCode);
        Assert.StartsWith("HTTP 400:", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Bad Request", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("correlationId:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_maps_cbms_404_to_policy_rejection()
    {
        // The cache throws ErrorResponse with 404; verify it is mapped to a policy rejection.
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        var errorResponse = new ErrorResponse
        {
            ErrorDetails =
            [
                new ErrorDetail { Code = "404", Message = "Not Found" }
            ],
            CorrelationId = "x",
            ResponseStatusCode = 404
        };
        fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<GetAccountDetailsResponse?>(_ => throw errorResponse);
        PluginCache.OverrideForTesting(fakeCache);

        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "8912650" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CBMS_404", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_routes_through_household_cache()
    {
        var fakeCache = BuildCacheWithMatchingCwin("12345");
        PluginCache.OverrideForTesting(fakeCache);

        var handler = new CardReplacementPipelineMessageHandler(
            patchResponseJson: """{"respCd":"00","respMsg":"Success"}""");
        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "12345" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        await fakeCache.Received(1).GetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_does_not_write_through_or_invalidate()
    {
        var fakeCache = BuildCacheWithMatchingCwin("12345");
        PluginCache.OverrideForTesting(fakeCache);

        var handler = new CardReplacementPipelineMessageHandler(
            patchResponseJson: """{"respCd":"00","respMsg":"Success"}""");
        var service = new ColoradoCardReplacementService(
            BuildMinimalHostProvider(),
            CbmsTestConfiguration(),
            handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "12345" }],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        await fakeCache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<GetAccountDetailsResponse>(), Arg.Any<CancellationToken>());
        await fakeCache.DidNotReceive().InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // DD-collision tests (DC-358)
    //
    // Mirrors the real captured CBMS shape: two rows for the same child sharing a
    // sebtChldCwin. The first is AP, the second is DD.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Two rows for the same child sharing a sebtChldCwin: one AP and one DD, with
    /// distinct (sebtAppId, sebtChldId) pairs. This is the data shape that triggers
    /// the cwin collision — a child certified through one path also has a denied-duplicate
    /// application on another.
    /// Tests differentiate the two real-world variants (auto-eligible-active vs
    /// application-based-active) by varying the <c>CaseRef</c> they pass to the service:
    /// auto-eligible passes null <c>ApplicationId</c>/<c>ApplicationStudentId</c>, exercising the
    /// cwin-fallback match path; application-based passes both, exercising the unique-pair match.
    /// </summary>
    private static string DDCollisionEnrollmentJson(
        int sharedCwin,
        int apAppId, int apChldId,
        int ddAppId, int ddChldId) =>
        $$"""
        {
          "stdntEnrollDtls": [
            {"sebtChldCwin":{{sharedCwin}},"sebtChldId":{{apChldId}},"sebtAppId":{{apAppId}},"stdntEligSts":"AP"},
            {"sebtChldCwin":{{sharedCwin}},"sebtChldId":{{ddChldId}},"sebtAppId":{{ddAppId}},"stdntEligSts":"DD"}
          ]
        }
        """;

    [Fact]
    public async Task RequestCardReplacementAsync_DDCollision_DIRCActive_matches_only_AP_row()
    {
        // Mirrors captured review2 data: AP row is auto-eligible (DIRC), so portal model
        // exposes null ApplicationId/ApplicationStudentId — frontend's CaseRef carries only the cwin.
        // Cwin matches both the AP and DD rows; the DD pre-filter must remove the DD row
        // so cwin-based matching lands on the AP row only.
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: DDCollisionEnrollmentJson(
                sharedCwin: 3575922,
                apAppId: 1199133, apChldId: 1200889,
                ddAppId: 1199934, ddChldId: 1201813));
        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs =
            [
                new CaseRef
                {
                    SummerEbtCaseId = "3575922",
                    ApplicationId = null,
                    ApplicationStudentId = null
                }
            ],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
        Assert.Equal(1, handler.LastPatchBodyElementCount);
        Assert.Contains("1199133", handler.LastPatchBodyRaw);
        Assert.Contains("1200889", handler.LastPatchBodyRaw);
        Assert.DoesNotContain("1199934", handler.LastPatchBodyRaw);
        Assert.DoesNotContain("1201813", handler.LastPatchBodyRaw);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_ApplicationBasedActive_with_DD_sibling_matches_by_appId_and_chldId()
    {
        // Hypothetical second shape: both AP and DD rows are application-based.
        // Frontend's CaseRef carries the populated (ApplicationId, ApplicationStudentId)
        // because the read-path mapper exposes them for application-based cases.
        // The (appId, chldId) match path uniquely identifies the AP row.
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: DDCollisionEnrollmentJson(
                sharedCwin: 7001,
                apAppId: 5001, apChldId: 6001,
                ddAppId: 5002, ddChldId: 6002));
        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs =
            [
                new CaseRef
                {
                    SummerEbtCaseId = "7001",
                    ApplicationId = "5001",
                    ApplicationStudentId = "6001"
                }
            ],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
        Assert.Equal(1, handler.LastPatchBodyElementCount);
        Assert.Contains("5001", handler.LastPatchBodyRaw);
        Assert.Contains("6001", handler.LastPatchBodyRaw);
        Assert.DoesNotContain("5002", handler.LastPatchBodyRaw);
        Assert.DoesNotContain("6002", handler.LastPatchBodyRaw);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_DDPreFilter_excludes_DD_row_even_when_appId_chldId_target_it()
    {
        // Defense-in-depth: even if a stale or malformed CaseRef carried the DD row's
        // (ApplicationId, ApplicationStudentId), the DD pre-filter removes that row
        // from the candidate pool before matching. Returns CASES_NOT_FOUND.
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: DDCollisionEnrollmentJson(
                sharedCwin: 1001,
                apAppId: 8001, apChldId: 9001,
                ddAppId: 8002, ddChldId: 9002));
        var service = BuildService(handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseRefs =
            [
                new CaseRef
                {
                    SummerEbtCaseId = "1001",
                    ApplicationId = "8002",
                    ApplicationStudentId = "9002"
                }
            ],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CASES_NOT_FOUND", result.ErrorCode);
        Assert.False(handler.ReceivedPatch);
    }

    // ---------------------------------------------------------------------------
    // Test handler
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns OAuth token JSON and a configurable PATCH update-std-dtls response.
    /// The get-account-details path is NOT handled here — those reads now come from
    /// the household cache (substituted via PluginCache.OverrideForTesting).
    /// </summary>
    private sealed class CardReplacementPipelineMessageHandler : HttpMessageHandler
    {
        private readonly string _patchResponseJson;

        public bool ReceivedPatch { get; private set; }
        public string LastPatchBodyRaw { get; private set; } = string.Empty;
        public int LastPatchBodyElementCount { get; private set; }

        /// <summary>
        /// The account-details JSON that was passed at construction; stored so
        /// <see cref="BuildService"/> can feed the same data to the cache substitute.
        /// </summary>
        internal string AccountDetailsJson { get; }

        public CardReplacementPipelineMessageHandler(
            string? accountDetailsJson = null,
            string? patchResponseJson = null,
            HttpStatusCode accountDetailsStatusCode = HttpStatusCode.OK)
        {
            // accountDetailsStatusCode is no longer used for HTTP interception since reads
            // come from the cache, but the parameter is kept for call-site compatibility.
            _ = accountDetailsStatusCode;
            AccountDetailsJson = accountDetailsJson
                ?? """{"stdntEnrollDtls":[{"sebtChldCwin":8912650,"sebtChldId":1,"sebtAppId":2}]}""";
            _patchResponseJson = patchResponseJson
                ?? """{"respCd":"200","respMsg":"Success"}""";
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            if (url.Contains("token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                const string tokenJson = """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""";
                return Json(HttpStatusCode.OK, tokenJson);
            }

            if (url.Contains("update-std-dtls", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Patch)
            {
                ReceivedPatch = true;
                LastPatchBodyRaw = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                LastPatchBodyElementCount = CountTopLevelArrayElements(LastPatchBodyRaw);
                return Json(HttpStatusCode.OK, _patchResponseJson);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }

        private static int CountTopLevelArrayElements(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? doc.RootElement.GetArrayLength()
                    : 1;
            }
            catch
            {
                return 0;
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
