using System.Text.Json;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;
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
                    CallMode = EnrollmentCallMode.Batch,
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
                        Match = new EnrollmentMatch
                        {
                            Strategy = EnrollmentMatchStrategy.AnyRowValueIn,
                            Field = "sebtEligSts",
                            ValueIn = new List<string> { "Y" },
                        },
                    },
                },
            },
        };

    // ---- Batch config, no expansion: single row per child, correlated by index ----

    private static StateBackendConfiguration BatchNoExpandConfiguration() =>
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
                    CallMode = EnrollmentCallMode.Batch,
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
                        Match = new EnrollmentMatch
                        {
                            Strategy = EnrollmentMatchStrategy.AnyRowValueIn,
                            Field = "eligible",
                            ValueIn = new List<string> { "true" },
                        },
                    },
                },
            },
        };

    // ---- CO REAL match: batch confidenceThreshold. Group a child's candidate rows by index, take
    // the max score, match iff max > threshold (strict). Threshold 90. ----

    private static StateBackendConfiguration CoConfidenceThresholdConfiguration() =>
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
                    CallMode = EnrollmentCallMode.Batch,
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
                        Match = new EnrollmentMatch
                        {
                            Strategy = EnrollmentMatchStrategy.ConfidenceThreshold,
                            ScoreField = "mtchCnfd",
                            Threshold = 90.0,
                        },
                    },
                },
            },
        };

    // ---- PerChild + confidenceThreshold: single result's score > threshold, no argmax. ----

    private static StateBackendConfiguration PerChildConfidenceThresholdConfiguration() =>
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
                    CallMode = EnrollmentCallMode.PerChild,
                    Request = new EnrollmentRequestBinding
                    {
                        Map = new Dictionary<string, string>
                        {
                            ["firstName"] = "firstName",
                            ["lastName"] = "lastName",
                            ["dob"] = "dateOfBirth",
                        },
                    },
                    Response = new EnrollmentResponseMapping
                    {
                        Root = "$",
                        Match = new EnrollmentMatch
                        {
                            Strategy = EnrollmentMatchStrategy.ConfidenceThreshold,
                            ScoreField = "mtchCnfd",
                            Threshold = 90.0,
                        },
                    },
                },
            },
        };

    // ---- DC-shaped config: PerChild fan-out. The driver loops the batch and makes ONE call per
    // child; each call returns a single result object { isEligible: bool }. No correlation index. ----

    private static StateBackendConfiguration DcPerChildConfiguration() =>
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
                    CallMode = EnrollmentCallMode.PerChild,
                    Request = new EnrollmentRequestBinding
                    {
                        // PerChild: no index — each call is one child.
                        Map = new Dictionary<string, string>
                        {
                            ["firstName"] = "firstName",
                            ["lastName"] = "lastName",
                            ["dob"] = "dateOfBirth",
                        },
                    },
                    Response = new EnrollmentResponseMapping
                    {
                        // The single result object is the response root.
                        Root = "$",
                        Match = new EnrollmentMatch
                        {
                            Strategy = EnrollmentMatchStrategy.AnyRowValueIn,
                            Field = "isEligible",
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

    // ---- CO REAL match: batch confidenceThreshold (argmax by score + STRICT `>` threshold) ----

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_ArgmaxPicksHigherRow_MatchesWhenBestExceedsThreshold()
    {
        // Arrange — 04/08 is transposable, so the request emits two candidate rows under index "1".
        // The backend returns two candidate rows for the child: the transposed row scores higher.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)) });

        // Entered row scores 40 (below threshold); the transposed candidate scores 95 (above).
        // Argmax must pick 95, and 95 > 90 → match.
        const string responseJson =
            """
            {
              "stdntDtls": [
                { "stdReqInd": "1", "mtchCnfd": 40.0 },
                { "stdReqInd": "1", "mtchCnfd": 95.0 }
              ]
            }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdConfiguration(), request, responseJson);

        // Assert — the higher-confidence candidate row wins the argmax and clears the threshold.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal("chk-1", child.CheckId);
        Assert.True(child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_BestBelowThreshold_NoMatch()
    {
        // Arrange — day 25 > 12, so no transposition candidate; a single row scoring below threshold.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": 85.0 } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdConfiguration(), request, responseJson);

        // Assert — best score 85 ≤ 90 → no match.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
    }

    [Theory]
    // STRICT `>`: exactly 90.0 is NOT a match; 90.01 is.
    [InlineData(90.0, false)]
    [InlineData(90.01, true)]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_StrictBoundaryAt90(double score, bool expectedMatch)
    {
        // Arrange — day 25 > 12, so exactly one row; its score sits on/just past the boundary.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        string responseJson =
            $$"""
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": {{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdConfiguration(), request, responseJson);

        // Assert — strict `>`: 90.0 → not a match; 90.01 → match.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal(expectedMatch, child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_MissingScore_NotAMatch()
    {
        // Arrange — the score field is absent on the row; a missing score is not a match.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1" } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdConfiguration(), request, responseJson);

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
    }

    // ---- PerChild + confidenceThreshold: single result's score > threshold, no argmax ----

    [Theory]
    [InlineData(95.0, true)]
    [InlineData(90.0, false)]
    public async Task CheckEnrollmentAsync_PerChildConfidenceThreshold_SingleResultScoreComparedStrictly(
        double score, bool expectedMatch)
    {
        // Arrange — one child, one call, one result object carrying the score.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        string responseJson =
            $$"""
            { "mtchCnfd": {{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            PerChildConfidenceThresholdConfiguration(), request, responseJson);

        // Assert — the single result composes the confidenceThreshold strategy with perChild mode.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal(expectedMatch, child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_BatchNoExpand_SingleRowRequest_StraightforwardMatch()
    {
        // Arrange — two children; no expansion, so one row each.
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
        (string body, EnrollmentCheckResult result) = await RunAsync(BatchNoExpandConfiguration(), request, responseJson);

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

    // ---- PerChild fan-out: the driver loops children, ONE call each, no correlation index ----

    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_MakesOneCallPerChild_AndAggregatesPerChildMatch()
    {
        // Arrange — two children. The driver must issue TWO separate HTTP calls, each bound from a
        // single child, each returning a single { isEligible } result object.
        var request = new EnrollmentCheckRequest(
            new[]
            {
                new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)),
                new EnrollmentChild("chk-2", "Alan", "Turing", new DateOnly(2016, 7, 3)),
            });

        var capturedBodies = new List<string>();
        var mockHttp = new MockHttpMessageHandler();
        // Respond by inspecting the single-child request body: Ada eligible, Alan not.
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/enrollment/check")
            .Respond(message =>
            {
                string body = message.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                capturedBodies.Add(body);
                bool eligible = body.Contains("Ada", StringComparison.Ordinal);
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{ "isEligible": {{(eligible ? "true" : "false")}} }""",
                        System.Text.Encoding.UTF8,
                        "application/json"),
                };
                return response;
            });

        var backend = new ConfigurableStateBackend(DcPerChildConfiguration(), mockHttp.ToHttpClient());

        // Act
        EnrollmentCheckResult result = await backend.CheckEnrollmentAsync(request);

        // Assert — TWO separate HTTP calls, one per child.
        Assert.Equal(2, capturedBodies.Count);

        // Each call's body is a SINGLE child object (not an array) with no correlation index.
        using JsonDocument first = JsonDocument.Parse(capturedBodies[0]);
        Assert.Equal(JsonValueKind.Object, first.RootElement.ValueKind);
        Assert.Equal("Ada", first.RootElement.GetProperty("firstName").GetString());
        Assert.False(first.RootElement.TryGetProperty("reqInd", out _));

        using JsonDocument second = JsonDocument.Parse(capturedBodies[1]);
        Assert.Equal("Alan", second.RootElement.GetProperty("firstName").GetString());

        // Aggregated per-child verdicts, in request order.
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("chk-1", result.Results[0].CheckId);
        Assert.True(result.Results[0].IsMatch);
        Assert.Equal("chk-2", result.Results[1].CheckId);
        Assert.False(result.Results[1].IsMatch);
    }

    // ---- fail-loud LOAD-time validation of the call-mode / index-field / expand combinations ----
    // These invalid enrollment configs now fail at config load (StateBackendConfigurationValidator),
    // not on first dispatch — so they assert the validator throws directly.

    [Fact]
    public void Validate_Batch_WithoutIndexField_Throws()
    {
        StateBackendConfiguration config = BatchNoExpandConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = config with
        {
            Operations = config.Operations with
            {
                EnrollmentCheck = operation with
                {
                    Request = operation.Request! with { IndexField = null },
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("indexField", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_PerChild_WithIndexField_Throws()
    {
        StateBackendConfiguration config = DcPerChildConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = config with
        {
            Operations = config.Operations with
            {
                EnrollmentCheck = operation with
                {
                    Request = operation.Request! with { IndexField = "reqInd" },
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("indexField", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_PerChild_WithExpand_ThrowsNotSupported()
    {
        StateBackendConfiguration config = DcPerChildConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = config with
        {
            Operations = config.Operations with
            {
                EnrollmentCheck = operation with
                {
                    Request = operation.Request! with { Expand = CandidateExpansion.TransposeMonthDay },
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ConfidenceThreshold_MissingScoreFieldOrThreshold_Throws()
    {
        StateBackendConfiguration config = CoConfidenceThresholdConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = config with
        {
            Operations = config.Operations with
            {
                EnrollmentCheck = operation with
                {
                    Response = operation.Response! with
                    {
                        Match = new EnrollmentMatch
                        {
                            // confidenceThreshold with NO scoreField/threshold → fail loud.
                            Strategy = EnrollmentMatchStrategy.ConfidenceThreshold,
                        },
                    },
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("scoreField", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AnyRowValueIn_MissingFieldOrValueIn_Throws()
    {
        StateBackendConfiguration config = BatchNoExpandConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = config with
        {
            Operations = config.Operations with
            {
                EnrollmentCheck = operation with
                {
                    Response = operation.Response! with
                    {
                        Match = new EnrollmentMatch
                        {
                            // anyRowValueIn with NO field/valueIn → fail loud.
                            Strategy = EnrollmentMatchStrategy.AnyRowValueIn,
                        },
                    },
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("valueIn", ex.Message, StringComparison.OrdinalIgnoreCase);
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
