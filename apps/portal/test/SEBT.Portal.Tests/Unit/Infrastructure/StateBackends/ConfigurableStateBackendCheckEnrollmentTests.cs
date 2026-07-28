using System.Text.Json;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendCheckEnrollmentTests
{
    // ---- CO-shaped config: DOB candidate expansion + eligibility-flag row match + fan-in ----

    private static StateBackendConfiguration CoConfiguration() =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "k" },
            Operations = new StateBackendOperations
            {
                EnrollmentCheck = new EnrollmentCheckOperationConfig
                {
                    Method = StateBackendHttpMethod.Post,
                    Path = "/sebt/check-enrollment",
                    Request = new EnrollmentRequestBinding
                    {
                        IndexField = "stdReqInd",
                        Expand = CandidateExpansion.TransposeMonthDay,
                        Map = new Dictionary<string, string>
                        {
                            ["firstName"] = "stdFirstName",
                            ["lastName"] = "stdLastName",
                            ["dob"] = "stdDob",
                        },
                    },
                    Response = new EnrollmentResponseMapping
                    {
                        Root = "$.stdntDtls",
                        IndexField = "stdReqInd",
                        MatchWhen = new EnrollmentMatchCondition
                        {
                            Field = "sebtEligSts",
                            ValueIn = new List<string> { "Y" },
                        },
                    },
                },
            },
        };

    // ---- DC-shaped config: single row per child, no expansion, straightforward eligibility flag ----

    private static StateBackendConfiguration DcConfiguration() =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "k" },
            Operations = new StateBackendOperations
            {
                EnrollmentCheck = new EnrollmentCheckOperationConfig
                {
                    Method = StateBackendHttpMethod.Post,
                    Path = "/enrollment/check",
                    Request = new EnrollmentRequestBinding
                    {
                        IndexField = "reqInd",
                        // No expansion: single row per child.
                        Map = new Dictionary<string, string>
                        {
                            ["firstName"] = "firstName",
                            ["lastName"] = "lastName",
                            ["dob"] = "dateOfBirth",
                        },
                    },
                    Response = new EnrollmentResponseMapping
                    {
                        Root = "$.results",
                        IndexField = "reqInd",
                        MatchWhen = new EnrollmentMatchCondition
                        {
                            Field = "eligible",
                            ValueIn = new List<string> { "true" },
                        },
                    },
                },
            },
        };

    private static async Task<(string Body, EnrollmentCheckResult Result)> RunAsync(
        StateBackendConfiguration configuration, EnrollmentCheckRequest request, string responseJson)
    {
        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/*")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", responseJson);

        var backend = new ConfigurableStateBackend(configuration, mockHttp.ToHttpClient());
        EnrollmentCheckResult result = await backend.CheckEnrollmentAsync(request);

        Assert.NotNull(capturedBody);
        return (capturedBody!, result);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoTransposableDob_EmitsTwoRowsUnderOneIndex_AndFansInTransposedMatch()
    {
        // Arrange — 04/08 is transposable (day 8 <= 12, swap 08/04 is a different valid date).
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)) });

        // Only the TRANSPOSED row (08/04) is eligible; the entered row (04/08) is not.
        const string responseJson =
            """
            {
              "stdntDtls": [
                { "stdReqInd": "1", "stdDob": "2015-04-08", "sebtEligSts": "N" },
                { "stdReqInd": "1", "stdDob": "2015-08-04", "sebtEligSts": "Y" }
              ]
            }
            """;

        // Act
        (string body, EnrollmentCheckResult result) = await RunAsync(CoConfiguration(), request, responseJson);

        // Assert — request emitted two rows for the one child, both under correlation index "1".
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement rows = document.RootElement;
        Assert.Equal(JsonValueKind.Array, rows.ValueKind);
        Assert.Equal(2, rows.GetArrayLength());

        Assert.Equal("1", rows[0].GetProperty("stdReqInd").GetString());
        Assert.Equal("2015-04-08", rows[0].GetProperty("stdDob").GetString());
        Assert.Equal("Dimple", rows[0].GetProperty("stdFirstName").GetString());

        Assert.Equal("1", rows[1].GetProperty("stdReqInd").GetString());
        Assert.Equal("2015-08-04", rows[1].GetProperty("stdDob").GetString());

        // Fan-in: the transposed row matched → the child is reported matched.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal("chk-1", child.CheckId);
        Assert.True(child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoNonTransposableDob_EmitsSingleRow_NoExpansion()
    {
        // Arrange — day 25 > 12, so no transposition candidate is emitted.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1", "stdDob": "2015-06-25", "sebtEligSts": "Y" } ] }
            """;

        // Act
        (string body, EnrollmentCheckResult result) = await RunAsync(CoConfiguration(), request, responseJson);

        // Assert — exactly one request row, no expansion.
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement rows = document.RootElement;
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("2015-06-25", rows[0].GetProperty("stdDob").GetString());

        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.True(child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_Dc_SingleRowRequest_StraightforwardMatch()
    {
        // Arrange — two children; DC config does no expansion, so one row each.
        var request = new EnrollmentCheckRequest(
            new[]
            {
                new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)),
                new EnrollmentChild("chk-2", "Alan", "Turing", new DateOnly(2016, 7, 3)),
            });

        const string responseJson =
            """
            {
              "results": [
                { "reqInd": "1", "eligible": "true" },
                { "reqInd": "2", "eligible": "false" }
              ]
            }
            """;

        // Act
        (string body, EnrollmentCheckResult result) = await RunAsync(DcConfiguration(), request, responseJson);

        // Assert — one row per child (no expansion), both under their own index.
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement rows = document.RootElement;
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("1", rows[0].GetProperty("reqInd").GetString());
        Assert.Equal("Ada", rows[0].GetProperty("firstName").GetString());
        Assert.Equal("2", rows[1].GetProperty("reqInd").GetString());

        Assert.Equal(2, result.Results.Count);
        Assert.Equal("chk-1", result.Results[0].CheckId);
        Assert.True(result.Results[0].IsMatch);
        Assert.Equal("chk-2", result.Results[1].CheckId);
        Assert.False(result.Results[1].IsMatch);
    }

    // ---- transposeMonthDay strategy unit behavior ----

    [Theory]
    // Transposable: swap yields a different valid date.
    [InlineData(2015, 4, 8, 2015, 8, 4)]
    [InlineData(2015, 1, 12, 2015, 12, 1)]
    public void TryTransposeMonthDay_ReturnsSwappedDate_WhenValidAndDifferent(
        int y, int m, int d, int ey, int em, int ed)
    {
        DateOnly? result = EnrollmentCandidateExpander.TryTransposeMonthDay(new DateOnly(y, m, d));
        Assert.Equal(new DateOnly(ey, em, ed), result);
    }

    [Theory]
    // Day > 12: the day can't serve as a month, so no valid swap.
    [InlineData(2015, 6, 25)]
    [InlineData(2015, 4, 13)]
    // Month == day: the swap is a no-op (not "different"), so no extra candidate.
    [InlineData(2015, 7, 7)]
    public void TryTransposeMonthDay_ReturnsNull_WhenInvalidOrSame(int y, int m, int d)
    {
        Assert.Null(EnrollmentCandidateExpander.TryTransposeMonthDay(new DateOnly(y, m, d)));
    }
}
