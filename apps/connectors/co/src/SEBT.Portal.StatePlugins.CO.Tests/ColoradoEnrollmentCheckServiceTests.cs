using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoEnrollmentCheckServiceTests
{
    [Fact]
    public async Task CheckEnrollmentAsync_WhenRequestHasNoChildren_ReturnsEmptyResults()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>()
        };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_WhenNoApiConfiguration_ThrowsInvalidOperationException()
    {
        // Explicitly blank out all three CBMS keys so the helper does not fall through to
        // CI env vars (Cbms__ClientId, Cbms__ClientSecret, Cbms__UseMockResponses) that
        // would either supply real credentials or enable mock mode — both suppress the
        // missing-credentials guard we're testing here.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:UseMockResponses"] = "false",
                ["Cbms:ClientId"] = "",
                ["Cbms:ClientSecret"] = ""
            })
            .Build();
        var service = new ColoradoEnrollmentCheckService(config);
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>
            {
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckEnrollmentAsync(request));
    }

    [Theory]
    [InlineData("Y", EnrollmentStatus.Match)]
    [InlineData("y", EnrollmentStatus.Match)]
    [InlineData("N", EnrollmentStatus.NonMatch)]
    [InlineData("n", EnrollmentStatus.NonMatch)]
    [InlineData(null, EnrollmentStatus.NonMatch)]
    [InlineData("", EnrollmentStatus.NonMatch)]
    [InlineData("UNEXPECTED", EnrollmentStatus.NonMatch)]
    public void MapEnrollmentStatus_MapsSebtEligStsValues(string? status, EnrollmentStatus expected)
    {
        var result = ColoradoEnrollmentCheckService.MapEnrollmentStatus(status);

        Assert.Equal(expected, result);
    }

    // ---------------------------------------------------------------------------
    // CorrelateResults: response correlation by stdReqInd, threshold, tie-break
    //
    // Tests target CorrelateResults directly and the outbound request shape via
    // BuildCbmsRequests rather than driving the full CheckEnrollmentAsync pipeline
    // through a mock HTTP handler. The service constructor only accepts
    // IConfiguration and ILoggerFactory, so capturing the outbound HTTP body would
    // require introducing a new test seam that the rest of the suite does not need.
    // ---------------------------------------------------------------------------

    private static ChildCheckRequest MakeRequest(
        string firstName,
        string lastName,
        DateOnly dob,
        Guid? checkId = null) =>
        new()
        {
            CheckId = checkId ?? Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dob
        };

    private static CheckEnrollmentStudentDetail MakeResponseRow(
        string? stdReqInd,
        double? mtchCnfd,
        string? sebtEligSts,
        string? stdFstNm = null,
        string? stdLstNm = null,
        string? stdDob = null)
    {
        var row = new CheckEnrollmentStudentDetail
        {
            StdReqInd = stdReqInd,
            MtchCnfd = mtchCnfd,
            StdFstNm = stdFstNm,
            StdLstNm = stdLstNm,
            StdDob = stdDob
        };
        if (sebtEligSts is not null)
        {
            row.AdditionalData["sebtEligSts"] = sebtEligSts;
        }
        return row;
    }

    private static Dictionary<string, ChildCheckRequest> IndexMap(params ChildCheckRequest[] children)
    {
        var map = new Dictionary<string, ChildCheckRequest>(StringComparer.Ordinal);
        for (var i = 0; i < children.Length; i++)
        {
            map[(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = children[i];
        }
        return map;
    }

    [Fact]
    public void CorrelateResults_ExactMatch_HighConfidence_Eligible_ReturnsMatch()
    {
        // Adela regression: input matches CBMS exactly, score 100, eligible.
        var child = MakeRequest("Adela", "Ramsden", new DateOnly(2008, 3, 17));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 100, "Y", "Adela", "Ramsden", "2008-03-17")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(100, result.MatchConfidence);
        Assert.Equal(child.CheckId, result.CheckId);
    }

    [Fact]
    public void CorrelateResults_TypoedLastName_AboveThreshold_Eligible_ReturnsMatch()
    {
        // Hettie repro: input has typo on last name; CBMS still scores 97 with
        // the correct canonical name. Pre-fix, this returned NonMatch + null.
        var child = MakeRequest("Hettie", "HAINSWORTHh", new DateOnly(2008, 3, 12));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 97, "Y", "Hettie", "HAINSWORTH", "2008-03-12")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(97, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_ScoreEqualToThreshold_ReturnsNonMatch_WithConfidence()
    {
        // Threshold operator is strict greater-than: a score of exactly 90.0 resolves to NonMatch.
        var child = MakeRequest("Test1", "Persona1", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 90.0, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(90.0, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_ScoreJustAboveThreshold_Eligible_ReturnsMatch()
    {
        // 90.5 is strictly greater than the 90 threshold, so the score gate passes and final status flows through the eligibility flag.
        var child = MakeRequest("Test2", "Persona2", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 90.5, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(90.5, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_LoweredThreshold_ScoreNowAboveThreshold_Eligible_ReturnsMatch()
    {
        // A score of 85 is sub-threshold at the default (90) but clears a configured 80, so the
        // score gate passes and the eligibility flag drives the final Match.
        var child = MakeRequest("Test4", "Persona4", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 85, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(
            children, response, IndexMap(child), matchConfidenceThreshold: 80.0);

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(85, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_RaisedThreshold_ScoreNowBelowThreshold_ReturnsNonMatch_ButPreservesScore()
    {
        // A score of 92 clears the default (90) but not a configured 95, so the score gate fails
        // and the child resolves to NonMatch while the score is still surfaced.
        var child = MakeRequest("Test5", "Persona5", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 92, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(
            children, response, IndexMap(child), matchConfidenceThreshold: 95.0);

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(92, result.MatchConfidence);
    }

    [Theory]
    [InlineData("80.5", 80.5)]
    [InlineData("0", 0.0)]
    [InlineData("100", 100.0)]
    public void ResolveMatchConfidenceThreshold_WhenValueInRange_ReturnsValue(string value, double expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:MatchConfidenceThreshold"] = value
            })
            .Build();

        var threshold = ColoradoEnrollmentCheckService.ResolveMatchConfidenceThreshold(configuration);

        Assert.Equal(expected, threshold);
    }

    [Fact]
    public void ResolveMatchConfidenceThreshold_WhenConfigurationNull_ReturnsDefault()
    {
        var threshold = ColoradoEnrollmentCheckService.ResolveMatchConfidenceThreshold(null);

        Assert.Equal(90.0, threshold);
    }

    [Fact]
    public void ResolveMatchConfidenceThreshold_WhenKeyAbsent_ReturnsDefault()
    {
        var configuration = new ConfigurationBuilder().Build();

        var threshold = ColoradoEnrollmentCheckService.ResolveMatchConfidenceThreshold(configuration);

        Assert.Equal(90.0, threshold);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    [InlineData("-0.1")]
    [InlineData("100.1")]
    [InlineData("900")]
    public void ResolveMatchConfidenceThreshold_WhenValueInvalid_Throws(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:MatchConfidenceThreshold"] = value
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => ColoradoEnrollmentCheckService.ResolveMatchConfidenceThreshold(configuration));
    }

    [Fact]
    public void CorrelateResults_SubThresholdScore_Eligible_ReturnsNonMatch_ButPreservesScore()
    {
        // Score 85 (sub-threshold). Status NonMatch but MatchConfidence is still
        // surfaced so logs/UI can explain how close we were.
        var child = MakeRequest("Test3", "Persona3", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 85, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(85, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_NullMtchCnfd_ReturnsNonMatch_WithNullConfidence()
    {
        var child = MakeRequest("Test4", "Persona4", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", null, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_NoRowsForChild_ReturnsNonMatch_WithNoMatchingRecordMessage()
    {
        var child = MakeRequest("Test5", "Persona5", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>()
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
        Assert.Contains("No matching record", result.StatusMessage);
    }

    [Fact]
    public void CorrelateResults_TwoRowsSameStdReqInd_BothEligible_HighestScoreWins()
    {
        var child = MakeRequest("Test6", "Persona6", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 92, "Y"),
                MakeResponseRow("1", 97, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(97, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_TwoRowsSameStdReqInd_HighestScoreIneligible_StatusFollowsBestRow()
    {
        // Tie-break across rows sharing a stdReqInd uses the highest mtchCnfd,
        // regardless of the row's eligibility flag. The winning row's status then
        // flows through MapEnrollmentStatus, so a 97-confidence row with
        // sebtEligSts="N" still resolves to NonMatch.
        var child = MakeRequest("Test7", "Persona7", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 92, "Y"),
                MakeResponseRow("1", 97, "N")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(97, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_TwoRowsBothNullScore_DeterministicNonMatch()
    {
        var child = MakeRequest("Test8", "Persona8", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", null, "Y"),
                MakeResponseRow("1", null, "N")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_OrphanRowsWithoutStdReqInd_AreDropped_ChildResolvesAsNonMatch()
    {
        // Response rows with null/empty StdReqInd are dropped from correlation
        // entirely, not name+DOB-fallback-matched. With no surviving rows for the
        // child's index, the result resolves to NonMatch.
        var child = MakeRequest("Test9", "Persona9", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow(null, 99, "Y", "Test9", "Persona9", "2010-01-01"),
                MakeResponseRow("", 95, "Y", "Test9", "Persona9", "2010-01-01")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_MultipleChildren_RowsInterleavedOutOfOrder_EachCorrelatesByStdReqInd()
    {
        var c1 = MakeRequest("Adela", "Ramsden", new DateOnly(2008, 3, 17));
        var c2 = MakeRequest("Hettie", "HAINSWORTHh", new DateOnly(2008, 3, 12));
        var c3 = MakeRequest("Test10", "Persona10", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { c1, c2, c3 };

        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                // Out-of-order: c3, c1, c2
                MakeResponseRow("3", 80, "N"),
                MakeResponseRow("1", 100, "Y"),
                MakeResponseRow("2", 97, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(c1, c2, c3));

        Assert.Equal(3, results.Count);

        Assert.Equal(c1.CheckId, results[0].CheckId);
        Assert.Equal(EnrollmentStatus.Match, results[0].Status);
        Assert.Equal(100, results[0].MatchConfidence);

        Assert.Equal(c2.CheckId, results[1].CheckId);
        Assert.Equal(EnrollmentStatus.Match, results[1].Status);
        Assert.Equal(97, results[1].MatchConfidence);

        Assert.Equal(c3.CheckId, results[2].CheckId);
        Assert.Equal(EnrollmentStatus.NonMatch, results[2].Status);
        Assert.Equal(80, results[2].MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_WhenCbmsReturnsNameAndDob_UsesCbmsValues()
    {
        // The connector must populate FirstName/LastName/DateOfBirth from the CBMS
        // response row, not from the submitted request. The portal-side filter compares
        // result vs request to detect fuzzy-match false positives — echoing submitted
        // values back defeats that check entirely.
        var child = MakeRequest("Jane", "Doe", new DateOnly(2015, 3, 12));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 95, "Y", stdFstNm: "JANET", stdLstNm: "SMITH", stdDob: "2015-07-22")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal("JANET", result.FirstName);
        Assert.Equal("SMITH", result.LastName);
        Assert.Equal(new DateOnly(2015, 7, 22), result.DateOfBirth);
    }

    [Fact]
    public void CorrelateResults_WhenCbmsOmitsNameAndDob_FallsBackToSubmittedValues()
    {
        // Defensive: if CBMS returns null for name/DOB, fall back to submitted so the
        // result stays usable. (The handler will replace with submitted values anyway,
        // but the fallback keeps the connector self-consistent.)
        var child = MakeRequest("Jane", "Doe", new DateOnly(2015, 3, 12));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 95, "Y", stdFstNm: null, stdLstNm: null, stdDob: null)
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal("Jane", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal(new DateOnly(2015, 3, 12), result.DateOfBirth);
    }

    // ---------------------------------------------------------------------------
    // Outbound request shape: per-row 1-based StdReqInd; names, DOB, school code
    // pass through unchanged. Tested via the BuildCbmsRequests helper for the
    // reason described in the comment above the correlation tests.
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildCbmsRequests_ThreeChildren_AssignsOneBasedStdReqInd()
    {
        // DOBs are deliberately non-transposable (day > 12) so this test stays focused on
        // 1-based StdReqInd assignment and field pass-through. Transposition expansion (where
        // one child yields two rows) is covered by the dedicated DC-500 tests below.
        var c1 = MakeRequest("Adela", "Ramsden", new DateOnly(2008, 3, 17));
        var c2 = MakeRequest("Hettie", "HAINSWORTHh", new DateOnly(2008, 3, 22));
        var c3 = MakeRequest("Test11", "Persona11", new DateOnly(2010, 6, 15));
        c3 = new ChildCheckRequest
        {
            CheckId = c3.CheckId,
            FirstName = c3.FirstName,
            LastName = c3.LastName,
            DateOfBirth = c3.DateOfBirth,
            SchoolCode = "12345"
        };
        var children = new List<ChildCheckRequest> { c1, c2, c3 };

        var requests = ColoradoEnrollmentCheckService.BuildCbmsRequests(children);

        Assert.Equal(3, requests.Count);

        Assert.Equal("1", requests[0].StdReqInd);
        Assert.Equal("Adela", requests[0].StdFirstName);
        Assert.Equal("Ramsden", requests[0].StdLastName);
        Assert.Equal("2008-03-17", requests[0].StdDob);

        Assert.Equal("2", requests[1].StdReqInd);
        Assert.Equal("Hettie", requests[1].StdFirstName);
        Assert.Equal("HAINSWORTHh", requests[1].StdLastName);
        Assert.Equal("2008-03-22", requests[1].StdDob);

        Assert.Equal("3", requests[2].StdReqInd);
        Assert.Equal("Test11", requests[2].StdFirstName);
        Assert.Equal("Persona11", requests[2].StdLastName);
        Assert.Equal("2010-06-15", requests[2].StdDob);
        Assert.Equal("12345", requests[2].StdSchlCd);
    }

    // ---------------------------------------------------------------------------
    // DC-500: Date transposition. When a child's DOB has a swappable month/day
    // (the swap yields a *different* valid calendar date), we submit a second CBMS
    // row for that child carrying the same StdReqInd but the month and day swapped.
    // This catches guardians who mis-enter a transposable date (e.g. 04/08 vs 08/04).
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(2010, 12, 1, "2010-01-12")]  // Dec 1  -> Jan 12 (ticket example)
    [InlineData(2008, 4, 8, "2008-08-04")]   // Apr 8  -> Aug 4  (Devora Robert)
    [InlineData(2010, 6, 1, "2010-01-06")]   // Jun 1  -> Jan 6
    [InlineData(2008, 3, 12, "2008-12-03")]  // Mar 12 -> Dec 3
    [InlineData(2020, 11, 11, null)]         // month == day -> swap is a no-op
    [InlineData(2008, 2, 13, null)]          // day 13 can't be a month
    [InlineData(2008, 3, 17, null)]          // day 17 can't be a month
    public void TryTransposeMonthAndDay_SwapsOnlyWhenResultIsValidAndDifferent(
        int year, int month, int day, string? expected)
    {
        var result = ColoradoEnrollmentCheckService.TryTransposeMonthAndDay(new DateOnly(year, month, day));

        if (expected is null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.Equal(
                DateOnly.ParseExact(expected, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                result);
        }
    }

    [Fact]
    public void BuildCbmsRequests_TransposableDob_EmitsTwoRowsWithSameStdReqInd_EnteredThenTransposed()
    {
        var child = MakeRequest("Devora", "Robert", new DateOnly(2008, 4, 8));

        var requests = ColoradoEnrollmentCheckService.BuildCbmsRequests(new List<ChildCheckRequest> { child });

        Assert.Equal(2, requests.Count);
        Assert.All(requests, r => Assert.Equal("1", r.StdReqInd));
        Assert.All(requests, r => Assert.Equal("Devora", r.StdFirstName));
        Assert.All(requests, r => Assert.Equal("Robert", r.StdLastName));
        // Entered DOB first, transposed second.
        Assert.Equal("2008-04-08", requests[0].StdDob);
        Assert.Equal("2008-08-04", requests[1].StdDob);
    }

    [Fact]
    public void BuildCbmsRequests_NonTransposableDob_EmitsSingleRow()
    {
        // Nov 11: swapping month/day yields the same date, so no second row.
        var child = MakeRequest("Nora", "Vance", new DateOnly(2020, 11, 11));

        var requests = ColoradoEnrollmentCheckService.BuildCbmsRequests(new List<ChildCheckRequest> { child });

        var row = Assert.Single(requests);
        Assert.Equal("1", row.StdReqInd);
        Assert.Equal("2020-11-11", row.StdDob);
    }

    [Fact]
    public void BuildCbmsRequests_MixedChildren_ExpandsOnlyTransposable_PreservingPerChildStdReqInd()
    {
        var transposable = MakeRequest("Devora", "Robert", new DateOnly(2008, 4, 8));    // 2 rows, StdReqInd "1"
        var nonTransposable = MakeRequest("Nora", "Vance", new DateOnly(2020, 11, 11));  // 1 row,  StdReqInd "2"
        var alsoTransposable = MakeRequest("Milo", "Quinn", new DateOnly(2010, 12, 1));  // 2 rows, StdReqInd "3"
        var children = new List<ChildCheckRequest> { transposable, nonTransposable, alsoTransposable };

        var requests = ColoradoEnrollmentCheckService.BuildCbmsRequests(children);

        Assert.Equal(5, requests.Count);

        var child1Rows = requests.Where(r => r.StdReqInd == "1").ToList();
        Assert.Equal(2, child1Rows.Count);
        Assert.Contains(child1Rows, r => r.StdDob == "2008-04-08");
        Assert.Contains(child1Rows, r => r.StdDob == "2008-08-04");

        var child2Row = Assert.Single(requests, r => r.StdReqInd == "2");
        Assert.Equal("2020-11-11", child2Row.StdDob);

        var child3Rows = requests.Where(r => r.StdReqInd == "3").ToList();
        Assert.Equal(2, child3Rows.Count);
        Assert.Contains(child3Rows, r => r.StdDob == "2010-12-01");
        Assert.Contains(child3Rows, r => r.StdDob == "2010-01-12");
    }
}
