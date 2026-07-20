using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Integration tests that call the CBMS sandbox (UAT) API to verify the Kiota-generated client
/// works end-to-end: OAuth 2.0 authentication, request construction, and response parsing.
/// Tests skip gracefully when no credentials are configured.
/// </summary>
/// <remarks>
/// Assertions are intentionally loose — we're validating auth + serialization wiring,
/// not sandbox business data. Tighten assertions after seeing what the sandbox returns.
/// </remarks>
[Collection("CbmsSandbox")]
public class CbmsSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

    [SkippableFact]
    public async Task Ping_ReturnsSuccessResponse()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);

        var response = await fixture.Client!.Ping.GetAsync();

        Assert.NotNull(response);
    }

    [SkippableFact]
    public async Task CheckEnrollment_ReturnsResponse()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);

        var request = new List<CheckEnrollmentRequest>
        {
            new()
            {
                StdFirstName = "REON",
                StdLastName = "NEBADA",
                StdDob = "2010-07-13",
                CbmsCsId = "",
                StdSasId = "",
                StdSchlCd = "",
                SebtYear = "2025",
                StdReqInd = "1",
            },
        };

        try
        {
            var response = await fixture.Client!.Sebt.CheckEnrollment.PostAsync(request);
            Assert.NotNull(response);

            // Verify sebtEligSts is returned (DC-287)
            var student = response.StdntDtls?.FirstOrDefault();
            if (student != null)
            {
                Assert.True(
                    student.AdditionalData.ContainsKey("sebtEligSts"),
                    "Expected sebtEligSts in check-enrollment response. " +
                    "CBMS may not be returning this field yet.");
            }
        }
        catch (ErrorResponse ex)
        {
            if (ex.ResponseStatusCode == 401)
                throw;

            Skip.If(
                true,
                "CBMS UAT rejected the placeholder check-enrollment payload (business/validation). " +
                "OAuth and CFA wiring still work — replace with known-good sandbox data if you need a strict pass. " +
                $"Status={ex.ResponseStatusCode}, Message={ex.Message}");
        }
    }

    [SkippableFact]
    public async Task GetAccountDetails_ReturnsValidResponse_WhenSandboxIsReachable()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.UseMockResponses, "Skipped when using mock responses; this test verifies the real sandbox response.");

        var request = new GetAccountDetailsRequest
        {
            PhnNm = "8185558437",
        };

        var response = await fixture.Client!.Sebt.GetAccountDetails.PostAsync(request);

        // Sandbox may return null or empty when no account exists for the phone
        if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
        {
            Skip.If(true, "Sandbox has no test data for phone 8185558437; cannot verify response structure.");
        }

        Assert.All(response!.StdntEnrollDtls!, student =>
        {
            Assert.NotNull(student);
            // Sandbox may return partial records; verify core identifiers when present
            if (student.SebtChldId.HasValue)
                Assert.True(student.SebtAppId.HasValue, "Student with SebtChldId must have SebtAppId.");
            // When StdDob is present, it must be parseable as a date (YYYY-MM-DD per spec)
            if (!string.IsNullOrEmpty(student.StdDob))
                Assert.True(DateOnly.TryParse(student.StdDob, out _), $"StdDob must be a valid date, got: {student.StdDob}");
            // When date fields are present, they must be parseable
            if (!string.IsNullOrEmpty(student.BenAvalDt))
                Assert.True(DateOnly.TryParse(student.BenAvalDt, out _), $"BenAvalDt must be a valid date, got: {student.BenAvalDt}");
            if (!string.IsNullOrEmpty(student.BenExpDt))
                Assert.True(DateOnly.TryParse(student.BenExpDt, out _), $"BenExpDt must be a valid date, got: {student.BenExpDt}");
            // EbtCardLastFour when present should be 4 digits
            if (!string.IsNullOrEmpty(student.EbtCardLastFour))
                Assert.Matches("^[0-9]{4}$", student.EbtCardLastFour);
        });
    }

    /// <summary>
    /// Live PATCH <c>update-std-dtls</c> with a real CBMS-shaped body (two array elements — UAT may accept or reject duplicates).
    /// Run: <c>dotnet test --filter "FullyQualifiedName~UpdateStdDtls_ReturnsSuccess_WhenUatAcceptsExampleBody"</c>
    /// with <c>Cbms:ClientId</c> / <c>Cbms:ClientSecret</c> and without <c>Cbms:UseMockResponses</c>.
    /// </summary>
    [SkippableFact]
    public async Task UpdateStdDtls_ReturnsSuccess_WhenUatAcceptsExampleBody()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.UseMockResponses, "Use real UAT credentials; mocks short-circuit PATCH without validating CBMS business rules.");

        const string json = """
            [{
                "sebtChldId": "1200507",
                "sebtAppId": "1198782",
                "addr": {
                    "addrLn1": "1480 S SEEME ST",
                    "addrLn2": "3",
                    "cty": "DENVER",
                    "staCd": "CO",
                    "zip": "80219"
                },
                "reqNewCard": "Y"
            },
            {
                "sebtChldId": "1200507",
                "sebtAppId": "1198782",
                "addr": {
                    "addrLn1": "1480 S SEEMETHREE ST",
                    "addrLn2": "3",
                    "cty": "DENVER",
                    "staCd": "CO",
                    "zip": "80219"
                },
                "reqNewCard": "Y"
            }]
            """;

        using var doc = JsonDocument.Parse(json);
        var bodies = new List<UpdateStudentDetailsRequest>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            bodies.Add((await KiotaJsonSerializer.DeserializeAsync<UpdateStudentDetailsRequest>(
                el.GetRawText(),
                UpdateStudentDetailsRequest.CreateFromDiscriminatorValue))!);
        }

        try
        {
            var response = await fixture.Client!.Sebt.UpdateStdDtls.PatchAsync(bodies);
            Assert.NotNull(response);
            Assert.True(
                ColoradoAddressUpdateService.IsCbmsUpdateSuccessCode(response.RespCd),
                $"Expected respCd 200 or 00 (UAT), got {response.RespCd}: {response.RespMsg}");
        }
        catch (ErrorResponse ex)
        {
            if (ex.ResponseStatusCode == 401)
                throw;

            Assert.Fail(
                "CBMS UAT rejected the example update-std-dtls payload. " +
                $"HTTP {(ex.ResponseStatusCode is { } code ? code.ToString() : "?")}, {ex.Message}. " +
                "Confirm ids and address with CBMS or reduce to a single array element if duplicates are invalid.");
        }
    }
}
