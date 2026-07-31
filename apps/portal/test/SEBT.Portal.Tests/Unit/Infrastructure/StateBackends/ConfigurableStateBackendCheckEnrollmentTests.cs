using System.Net;
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
    // Rebuilds the config with its enrollment-check operation swapped out.
    private static StateBackendConfiguration WithEnrollment(
        StateBackendConfiguration config, EnrollmentCheckOperationConfig operation) =>
        config with
        {
            Operations = config.Operations with { EnrollmentCheck = operation },
        };

    // CO-shaped: batch, DOB expansion, eligibility-flag row match.
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

    // Batch, no expansion: single row per child, correlated by index.
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

    // Batch confidenceThreshold: argmax a child's candidate scores, match iff best > threshold (strict).
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

    // CO's REAL rule: best-candidate score > threshold AND that SAME row's eligibility flag is set.
    private static StateBackendConfiguration CoConfidenceThresholdWithEligibilityConfiguration()
    {
        StateBackendConfiguration config = CoConfidenceThresholdConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        return WithEnrollment(config, operation with
        {
            Response = operation.Response! with
            {
                Match = operation.Response.Match with
                {
                    Field = "sebtEligSts",
                    ValueIn = new List<string> { "Y" },
                },
            },
        });
    }

    // PerChild + confidenceThreshold: single result's score > threshold, no argmax.
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

    // DC-shaped: PerChild fan-out, one call per child, single { isEligible } result, no index.
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

        // Fan-in: any matching candidate row matches the child.
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
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_ArgmaxPicksHigherRow_MatchesWhenBestExceedsThreshold()
    {
        // Arrange — 04/08 is transposable, so the child has two candidate rows under index "1".
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)) });

        // Entered row scores 40, transposed candidate 95: argmax picks 95, and 95 > 90 → match.
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

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal("chk-1", child.CheckId);
        Assert.True(child.IsMatch);
    }

    // Strict `>`: clearly below and exactly 90.0 do NOT match; 90.01 does.
    [Theory]
    [InlineData(85.0, false)]
    [InlineData(90.0, false)]
    [InlineData(90.01, true)]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_StrictBoundaryAt90(double score, bool expectedMatch)
    {
        // Arrange — day 25 > 12, so exactly one row; its score sits below/on/just past the boundary.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        string responseJson =
            $$"""
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": {{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdConfiguration(), request, responseJson);

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal(expectedMatch, child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_MissingScore_NotAMatch()
    {
        // Arrange — a missing score is not a match.
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

    // A confident candidate that is NOT SEBT-eligible must not report a match.
    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThresholdWithEligibility_ConfidentButIneligible_NotAMatch()
    {
        // Arrange
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        // Score clears the threshold (95 > 90), but the row's eligibility flag says "N".
        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": 95.0, "sebtEligSts": "N" } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdWithEligibilityConfiguration(), request, responseJson);

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThresholdWithEligibility_ConfidentAndEligible_Matches()
    {
        // Arrange
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": 95.0, "sebtEligSts": "Y" } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdWithEligibilityConfiguration(), request, responseJson);

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.True(child.IsMatch);
    }

    // Eligibility is read from the ARGMAX row only — a lower-scoring eligible candidate cannot
    // rescue an ineligible best row (mirrors the CO plugin: argmax first, then eligibility).
    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThresholdWithEligibility_LowerEligibleRowCannotRescueIneligibleArgmax()
    {
        // Arrange — 04/08 is transposable, so the child has two candidate rows under index "1".
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)) });

        // Both rows clear the threshold, but the argmax row (95) is ineligible.
        const string responseJson =
            """
            {
              "stdntDtls": [
                { "stdReqInd": "1", "mtchCnfd": 95.0, "sebtEligSts": "N" },
                { "stdReqInd": "1", "mtchCnfd": 92.0, "sebtEligSts": "Y" }
              ]
            }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdWithEligibilityConfiguration(), request, responseJson);

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
    }

    // PerChild: the single result object must pass BOTH the score and the eligibility check.
    [Theory]
    [InlineData(95.0, "Y", true)]
    [InlineData(95.0, "N", false)]
    [InlineData(90.0, "Y", false)]
    public async Task CheckEnrollmentAsync_PerChildConfidenceThresholdWithEligibility_BothChecksOnSingleResult(
        double score, string eligibility, bool expectedMatch)
    {
        // Arrange
        StateBackendConfiguration config = PerChildConfidenceThresholdConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Response = operation.Response! with
            {
                Match = operation.Response.Match with
                {
                    Field = "sebtEligSts",
                    ValueIn = new List<string> { "Y" },
                },
            },
        });
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        string responseJson =
            $$"""
            { "mtchCnfd": {{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "sebtEligSts": "{{eligibility}}" }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, responseJson);

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.Equal(expectedMatch, child.IsMatch);
    }

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

        // Assert
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

        // Assert — one row per child (no expansion), each under its own index.
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

    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_MakesOneCallPerChild_AndAggregatesPerChildMatch()
    {
        // Arrange — two children; PerChild must issue two separate single-child HTTP calls.
        var request = new EnrollmentCheckRequest(
            new[]
            {
                new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)),
                new EnrollmentChild("chk-2", "Alan", "Turing", new DateOnly(2016, 7, 3)),
            });

        var capturedBodies = new List<string>();
        var mockHttp = new MockHttpMessageHandler();
        // Ada eligible, Alan not.
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

        // Assert — two calls, each a single child object (not an array) with no correlation index.
        Assert.Equal(2, capturedBodies.Count);

        using JsonDocument first = JsonDocument.Parse(capturedBodies[0]);
        Assert.Equal(JsonValueKind.Object, first.RootElement.ValueKind);
        Assert.Equal("Ada", first.RootElement.GetProperty("firstName").GetString());
        Assert.False(first.RootElement.TryGetProperty("reqInd", out _));

        using JsonDocument second = JsonDocument.Parse(capturedBodies[1]);
        Assert.Equal("Alan", second.RootElement.GetProperty("firstName").GetString());

        // Verdicts aggregated in request order.
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("chk-1", result.Results[0].CheckId);
        Assert.True(result.Results[0].IsMatch);
        Assert.Equal("chk-2", result.Results[1].CheckId);
        Assert.False(result.Results[1].IsMatch);
    }

    // Rebuilds the DC PerChild config with its request binding swapped out.
    private static StateBackendConfiguration DcPerChildWithRequest(EnrollmentRequestBinding request)
    {
        StateBackendConfiguration config = DcPerChildConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        return WithEnrollment(config, operation with { Request = request });
    }

    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_MapOptional_BindsWhenInputResolves()
    {
        // Arrange — lastName moves from map to mapOptional; it still resolves, so it still binds.
        StateBackendConfiguration config = DcPerChildWithRequest(
            new EnrollmentRequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["firstName"] = "firstName",
                    ["dob"] = "dateOfBirth",
                },
                MapOptional = new Dictionary<string, string>
                {
                    ["lastName"] = "surname",
                },
            });
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        // Act
        (string body, _) = await RunAsync(config, request, """{ "isEligible": true }""");

        // Assert — the optional input binds to its target path exactly like a required one.
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("Ada", document.RootElement.GetProperty("firstName").GetString());
        Assert.Equal("Lovelace", document.RootElement.GetProperty("surname").GetString());
    }

    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_MapOptional_BindsSchoolIdentifierWhenChildCarriesIt()
    {
        // Arrange — the child carries a schoolIdentifier, so the optional input resolves and binds.
        StateBackendConfiguration config = DcPerChildWithRequest(
            new EnrollmentRequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["firstName"] = "firstName",
                    ["lastName"] = "lastName",
                    ["dob"] = "dateOfBirth",
                },
                MapOptional = new Dictionary<string, string>
                {
                    ["schoolIdentifier"] = "schlNm",
                },
            });
        var request = new EnrollmentCheckRequest(
            new[]
            {
                new EnrollmentChild(
                    "chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25), "Anacostia Elementary"),
            });

        // Act
        (string body, _) = await RunAsync(config, request, """{ "isEligible": true }""");

        // Assert
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("Anacostia Elementary", document.RootElement.GetProperty("schlNm").GetString());
    }

    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_MapOptional_OmitsFieldWhenInputDoesNotResolve()
    {
        // Arrange — the child carries no schoolIdentifier, so the optional input resolves to nothing.
        StateBackendConfiguration config = DcPerChildWithRequest(
            new EnrollmentRequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["firstName"] = "firstName",
                    ["lastName"] = "lastName",
                    ["dob"] = "dateOfBirth",
                },
                MapOptional = new Dictionary<string, string>
                {
                    ["schoolIdentifier"] = "schlNm",
                },
            });
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        // Act
        (string body, _) = await RunAsync(config, request, """{ "isEligible": true }""");

        // Assert — the optional field is omitted ENTIRELY, never written as null.
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("Ada", document.RootElement.GetProperty("firstName").GetString());
        Assert.False(document.RootElement.TryGetProperty("schlNm", out _));
    }

    // A mapOptional section must not relax the required map's fail-loud contract.
    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_RequiredMapStillThrowsOnUnknownInput()
    {
        // Arrange — middleName is not a child field at all, so the REQUIRED map must fail loud.
        StateBackendConfiguration config = DcPerChildWithRequest(
            new EnrollmentRequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["firstName"] = "firstName",
                    ["middleName"] = "midNm",
                },
                MapOptional = new Dictionary<string, string>
                {
                    ["lastName"] = "surname",
                },
            });
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/enrollment/check")
            .Respond("application/json", """{ "isEligible": true }""");
        var backend = new ConfigurableStateBackend(config, mockHttp.ToHttpClient());

        // Act + Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.CheckEnrollmentAsync(request));
        Assert.Contains("middleName", ex.Message);
    }

    // schoolIdentifier is a KNOWN child field but nullable — in the required map, a child without
    // one must fail loud, never silently drop the field.
    [Fact]
    public async Task CheckEnrollmentAsync_PerChild_RequiredMapSchoolIdentifier_ThrowsWhenChildHasNone()
    {
        StateBackendConfiguration config = DcPerChildWithRequest(
            new EnrollmentRequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["firstName"] = "firstName",
                    ["lastName"] = "lastName",
                    ["dob"] = "dateOfBirth",
                    ["schoolIdentifier"] = "schlNm",
                },
            });
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/enrollment/check")
            .Respond("application/json", """{ "isEligible": true }""");
        var backend = new ConfigurableStateBackend(config, mockHttp.ToHttpClient());

        // Act + Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.CheckEnrollmentAsync(request));
        Assert.Contains("schoolIdentifier", ex.Message);
    }

    // Batch rows honor the same optional-map semantics as PerChild bodies.
    [Fact]
    public async Task CheckEnrollmentAsync_Batch_MapOptional_BindsResolvedAndOmitsUnresolved()
    {
        // Arrange
        StateBackendConfiguration config = BatchNoExpandConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Request = operation.Request! with
            {
                MapOptional = new Dictionary<string, string>
                {
                    ["lastName"] = "surname",
                    ["schoolIdentifier"] = "schlNm",
                },
            },
        });
        var request = new EnrollmentCheckRequest(
            new[]
            {
                new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)),
                new EnrollmentChild("chk-2", "Alan", "Turing", new DateOnly(2016, 7, 3), "Bletchley Primary"),
            });

        const string responseJson =
            """
            {
              "results": [
                { "reqInd": "1", "eligible": "true" },
                { "reqInd": "2", "eligible": "true" }
              ]
            }
            """;

        // Act
        (string body, _) = await RunAsync(config, request, responseJson);

        // Assert — optional binding is decided PER ROW: omitted for the child without a value,
        // bound for the child with one.
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement firstRow = document.RootElement[0];
        Assert.Equal("Lovelace", firstRow.GetProperty("surname").GetString());
        Assert.False(firstRow.TryGetProperty("schlNm", out _));

        JsonElement secondRow = document.RootElement[1];
        Assert.Equal("Turing", secondRow.GetProperty("surname").GetString());
        Assert.Equal("Bletchley Primary", secondRow.GetProperty("schlNm").GetString());
    }

    // --- Result carriers: MatchConfidence, StatusMessage, and the result-level Message ---

    // Rebuilds a config with the response mapping's optional carrier fields set.
    private static StateBackendConfiguration WithResponseCarriers(
        StateBackendConfiguration config, string? statusMessageField, string? messageField)
    {
        var operation = config.Operations.EnrollmentCheck!;
        return WithEnrollment(config, operation with
        {
            Response = operation.Response! with
            {
                StatusMessageField = statusMessageField,
                MessageField = messageField,
            },
        });
    }

    // Mirrors the CO plugin: the winning row's confidence is reported even on the sub-threshold
    // non-match path, so callers can surface the score that was computed.
    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_BelowThreshold_StillReportsWinningConfidence()
    {
        // Arrange
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": 40.0 } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            CoConfidenceThresholdConfiguration(), request, responseJson);

        // Assert — no match, but the computed confidence rides along; unconfigured carriers stay null.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
        Assert.Equal(40.0, child.MatchConfidence);
        Assert.Null(child.StatusMessage);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_ReportsArgmaxRowConfidence()
    {
        // Arrange — 04/08 is transposable, so the child has two candidate rows under index "1".
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)) });

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

        // Assert — the argmax row's score is the reported confidence.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.True(child.IsMatch);
        Assert.Equal(95.0, child.MatchConfidence);
    }

    // StatusMessage reads the ARGMAX row even when its eligibility flag fails the match — mirrors
    // the CO plugin, which reports the best row's sebtEligSts regardless of the verdict.
    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_StatusMessageField_ReadsArgmaxRowEvenWhenIneligible()
    {
        // Arrange
        StateBackendConfiguration config = WithResponseCarriers(
            CoConfidenceThresholdWithEligibilityConfiguration(), "sebtEligSts", null);
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)) });

        const string responseJson =
            """
            {
              "stdntDtls": [
                { "stdReqInd": "1", "mtchCnfd": 95.0, "sebtEligSts": "N" },
                { "stdReqInd": "1", "mtchCnfd": 92.0, "sebtEligSts": "Y" }
              ]
            }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, responseJson);

        // Assert — the ineligible argmax row (95, "N") supplies BOTH carriers.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
        Assert.Equal(95.0, child.MatchConfidence);
        Assert.Equal("N", child.StatusMessage);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_CoConfidenceThreshold_NoRowsForChild_AllCarriersNull()
    {
        // Arrange — the backend returned no rows for the child at all.
        StateBackendConfiguration config = WithResponseCarriers(
            CoConfidenceThresholdConfiguration(), "sebtEligSts", null);
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, """{ "stdntDtls": [] }""");

        // Assert — no winning row, so nothing to carry.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
        Assert.Null(child.MatchConfidence);
        Assert.Null(child.StatusMessage);
    }

    // The result-level message reads from the response DOCUMENT root, not the rows under `root`.
    [Fact]
    public async Task CheckEnrollmentAsync_Batch_MessageField_ReadsResponseDocumentRoot()
    {
        // Arrange
        StateBackendConfiguration config = WithResponseCarriers(
            CoConfidenceThresholdConfiguration(), null, "RespMsg");
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "RespMsg": "Success", "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": 95.0 } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, responseJson);

        // Assert
        Assert.Equal("Success", result.Message);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_Batch_MessageField_AbsentProperty_NullMessage()
    {
        // Arrange — messageField is configured, but the response carries no such property.
        StateBackendConfiguration config = WithResponseCarriers(
            CoConfidenceThresholdConfiguration(), null, "RespMsg");
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "stdntDtls": [ { "stdReqInd": "1", "mtchCnfd": 95.0 } ] }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, responseJson);

        // Assert
        Assert.Null(result.Message);
    }

    // anyRowValueIn has no score, so MatchConfidence stays null; StatusMessage reads the FIRST
    // MATCHING row (a non-matching child has no winning row, so its carrier stays null).
    [Fact]
    public async Task CheckEnrollmentAsync_CoAnyRowValueIn_NullConfidence_StatusMessageFromFirstMatchingRow()
    {
        // Arrange
        StateBackendConfiguration config = WithResponseCarriers(CoConfiguration(), "eligMsg", null);
        var request = new EnrollmentCheckRequest(
            new[]
            {
                new EnrollmentChild("chk-1", "Dimple", "Wibert", new DateOnly(2015, 4, 8)),
                new EnrollmentChild("chk-2", "Ada", "Lovelace", new DateOnly(2015, 6, 25)),
            });

        const string responseJson =
            """
            {
              "stdntDtls": [
                { "stdReqInd": "1", "sebtEligSts": "N", "eligMsg": "not enrolled" },
                { "stdReqInd": "1", "sebtEligSts": "Y", "eligMsg": "enrolled" },
                { "stdReqInd": "2", "sebtEligSts": "N", "eligMsg": "not enrolled" }
              ]
            }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, responseJson);

        // Assert — child 1 matched: its first MATCHING row (not its first row) supplies the message.
        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].IsMatch);
        Assert.Null(result.Results[0].MatchConfidence);
        Assert.Equal("enrolled", result.Results[0].StatusMessage);

        // Child 2 never matched: no winning row, so no status message.
        Assert.False(result.Results[1].IsMatch);
        Assert.Null(result.Results[1].StatusMessage);
    }

    // PerChild: the single result object supplies all three carriers for its one child.
    [Fact]
    public async Task CheckEnrollmentAsync_PerChildConfidenceThreshold_CarriersReadFromSingleResult()
    {
        // Arrange
        StateBackendConfiguration config = WithResponseCarriers(
            PerChildConfidenceThresholdConfiguration(), "sebtEligSts", "RespMsg");
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        const string responseJson =
            """
            { "mtchCnfd": 40.0, "sebtEligSts": "N", "RespMsg": "OK" }
            """;

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(config, request, responseJson);

        // Assert — sub-threshold non-match still carries the computed score + status message.
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
        Assert.Equal(40.0, child.MatchConfidence);
        Assert.Equal("N", child.StatusMessage);
        Assert.Equal("OK", result.Message);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_PerChildAnyRowValueIn_UnconfiguredCarriersStayNull()
    {
        // Arrange — DC-shaped config: no scoreField, no carrier fields configured.
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        // Act
        (_, EnrollmentCheckResult result) = await RunAsync(
            DcPerChildConfiguration(), request, """{ "isEligible": true }""");

        // Assert
        EnrollmentChildResult child = Assert.Single(result.Results);
        Assert.True(child.IsMatch);
        Assert.Null(child.MatchConfidence);
        Assert.Null(child.StatusMessage);
        Assert.Null(result.Message);
    }

    // Incoherent call-mode / index-field / expand combinations fail loud at config load.

    [Fact]
    public void Validate_Batch_WithoutIndexField_Throws()
    {
        StateBackendConfiguration config = BatchNoExpandConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Request = operation.Request! with { IndexField = null },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("indexField", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_PerChild_WithIndexField_Throws()
    {
        StateBackendConfiguration config = DcPerChildConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Request = operation.Request! with { IndexField = "reqInd" },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("indexField", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_PerChild_WithExpand_ThrowsNotSupported()
    {
        StateBackendConfiguration config = DcPerChildConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Request = operation.Request! with { Expand = CandidateExpansion.TransposeMonthDay },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ConfidenceThreshold_MissingScoreFieldOrThreshold_Throws()
    {
        StateBackendConfiguration config = CoConfidenceThresholdConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Response = operation.Response! with
            {
                Match = new EnrollmentMatch
                {
                    // confidenceThreshold with NO scoreField/threshold → fail loud.
                    Strategy = EnrollmentMatchStrategy.ConfidenceThreshold,
                },
            },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("scoreField", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // The optional eligibility check on confidenceThreshold is field + valueIn TOGETHER or neither.
    [Theory]
    [InlineData("sebtEligSts", null)]
    [InlineData(null, "Y")]
    public void Validate_ConfidenceThreshold_EligibilityParamsSuppliedAlone_Throws(
        string? field, string? valueIn)
    {
        StateBackendConfiguration config = CoConfidenceThresholdConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Response = operation.Response! with
            {
                Match = operation.Response.Match with
                {
                    Field = field,
                    ValueIn = valueIn is null ? null : new List<string> { valueIn },
                },
            },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("valueIn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AnyRowValueIn_MissingFieldOrValueIn_Throws()
    {
        StateBackendConfiguration config = BatchNoExpandConfiguration();
        var operation = config.Operations.EnrollmentCheck!;
        config = WithEnrollment(config, operation with
        {
            Response = operation.Response! with
            {
                Match = new EnrollmentMatch
                {
                    // anyRowValueIn with NO field/valueIn → fail loud.
                    Strategy = EnrollmentMatchStrategy.AnyRowValueIn,
                },
            },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("valueIn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A swap is emitted only when it yields a different valid date.
    [Theory]
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

    // Pins the current read-path contract: a non-2xx enrollment response throws a raw
    // HttpRequestException (no error mapping, no partial result). A future error-mapping
    // design must change this test deliberately.
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task CheckEnrollmentAsync_NonSuccessStatus_ThrowsHttpRequestException(
        HttpStatusCode status)
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/enrollment/check")
            .Respond(status);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BatchNoExpandConfiguration(), httpClient);
        var request = new EnrollmentCheckRequest(
            new[] { new EnrollmentChild("chk-1", "Ada", "Lovelace", new DateOnly(2015, 6, 25)) });

        // Act + Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => backend.CheckEnrollmentAsync(request));
    }
}
