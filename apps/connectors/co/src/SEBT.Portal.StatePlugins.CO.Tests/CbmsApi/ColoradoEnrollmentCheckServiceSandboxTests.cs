using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Opt-in live verification against CBMS UAT for the DC-500 date-transposition feature.
/// When a child's DOB is transposable, the connector submits two check-enrollment rows under
/// the same <c>stdReqInd</c> (the entered DOB plus its month/day-swapped candidate). CBMS finds
/// the real record and returns the matched row carrying that same <c>stdReqInd</c>, so
/// <see cref="ColoradoEnrollmentCheckService.CorrelateResults"/> groups it back to the child.
///
/// Findings confirmed against CBMS UAT:
///   - CBMS returns only the MATCHED row, not one row per submitted DOB.
///   - Test user Devora Robert's real DOB is 2008-04-08 (matches at confidence 100, eligible).
///     A guardian who transposes month/day enters 2008-08-04; the connector's transposed
///     candidate (2008-04-08) is what recovers the match.
///
/// Skips when no UAT credentials are configured (same as <see cref="CbmsSandboxFixture"/>) and
/// when mock responses are enabled (the mock store returns static data, not real CBMS match logic).
/// Run: <c>dotnet test --filter "FullyQualifiedName~ColoradoEnrollmentCheckServiceSandboxTests"</c>
/// with real <c>Cbms:ClientId</c> / <c>Cbms:ClientSecret</c> and without <c>Cbms:UseMockResponses</c>.
/// </summary>
[Collection("CbmsSandbox")]
public class ColoradoEnrollmentCheckServiceSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

    private const string MockSkipReason =
        "Use real UAT credentials; mock responses return static data and cannot exercise CBMS match logic.";

    private const string FirstName = "Devora";
    private const string LastName = "Robert";
    private static readonly DateOnly RealDob = new(2008, 4, 8);      // Devora's actual CBMS DOB (matches at 100)
    private static readonly DateOnly MistypedDob = new(2008, 8, 4);  // month/day transposed: what a confused guardian enters

    /// <summary>
    /// The feature working: a guardian enters the transposed (wrong) order 2008-08-04. The connector
    /// also submits the transposed candidate 2008-04-08, which is Devora's real DOB, so CBMS matches
    /// and the result resolves to her real record.
    /// </summary>
    [SkippableFact]
    public async Task CheckEnrollment_GuardianMistypedDob_MatchesViaTransposedCandidate()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.UseMockResponses, MockSkipReason);

        var child = MakeChild(MistypedDob);

        // Production request-building: the mistyped DOB expands to two rows (entered + transposed)
        // under the same stdReqInd "1".
        var cbmsRequests = ColoradoEnrollmentCheckService.BuildCbmsRequests(
            new List<ChildCheckRequest> { child });
        Assert.Equal(2, cbmsRequests.Count);
        Assert.All(cbmsRequests, r => Assert.Equal("1", r.StdReqInd));
        Assert.Equal("2008-08-04", cbmsRequests[0].StdDob);  // entered (mistyped)
        Assert.Equal("2008-04-08", cbmsRequests[1].StdDob);  // transposed candidate (her real DOB)

        CheckEnrollmentResponse? response = null;
        try
        {
            response = await fixture.Client!.Sebt.CheckEnrollment.PostAsync(cbmsRequests);
        }
        catch (ErrorResponse ex)
        {
            FailOnError(ex, "two-rows-same-stdReqInd");
        }

        Assert.NotNull(response);
        var studentDetails = response!.StdntDtls ?? new List<CheckEnrollmentStudentDetail>();
        Skip.If(
            studentDetails.Count == 0,
            "CBMS UAT returned no student details for Devora Robert; confirm she is a seeded enrollment.");

        // CBMS returns only the MATCHED row; what's load-bearing is that it echoes the stdReqInd on
        // that row so CorrelateResults can group it back to the child by index.
        var rowsForChild = studentDetails.Where(r => r.StdReqInd == "1").ToList();
        Assert.True(
            rowsForChild.Count >= 1,
            $"Expected CBMS to echo stdReqInd \"1\" on the matched row, " +
            $"got {rowsForChild.Count} row(s) under \"1\" out of {studentDetails.Count} total.");

        var results = ColoradoEnrollmentCheckService.CorrelateResults(
            new List<ChildCheckRequest> { child }, response, IndexMap(child));
        var result = Assert.Single(results);

        var actuals = Describe(rowsForChild, result);
        Assert.True(
            result.DateOfBirth == RealDob,
            $"Expected the transposed candidate {RealDob:yyyy-MM-dd} (Devora's real DOB) to win. {actuals}");
        Assert.True(
            result.Status == EnrollmentStatus.Match,
            $"Expected the recovered match to resolve to a Match. {actuals}");
        Assert.True(
            result.MatchConfidence > 90.0,
            $"Expected the winning row to clear the 90 match-confidence threshold. {actuals}");
    }

    /// <summary>
    /// The failure DC-500 fixes: the same mistyped DOB 2008-08-04 submitted ALONE (no transposed
    /// candidate, i.e. pre-DC-500 behavior) does not match Devora. If this matches, CBMS tolerates
    /// the transposition on its own and the feature would be unnecessary.
    /// </summary>
    [SkippableFact]
    public async Task CheckEnrollment_MistypedDobAlone_DoesNotMatch()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.UseMockResponses, MockSkipReason);

        var child = MakeChild(MistypedDob);

        // Pre-DC-500 baseline: a single row with only the entered (mistyped) DOB, no transposition.
        var singleRow = new List<CheckEnrollmentRequest>
        {
            new()
            {
                StdFirstName = FirstName,
                StdLastName = LastName,
                StdDob = "2008-08-04",
                StdReqInd = "1"
            }
        };

        CheckEnrollmentResponse? response = null;
        try
        {
            response = await fixture.Client!.Sebt.CheckEnrollment.PostAsync(singleRow);
        }
        catch (ErrorResponse ex)
        {
            FailOnError(ex, "single mistyped-DOB row");
        }

        Assert.NotNull(response);
        var rowsForChild = (response!.StdntDtls ?? new List<CheckEnrollmentStudentDetail>())
            .Where(r => r.StdReqInd == "1")
            .ToList();

        var results = ColoradoEnrollmentCheckService.CorrelateResults(
            new List<ChildCheckRequest> { child }, response, IndexMap(child));
        var result = Assert.Single(results);

        Assert.True(
            result.Status != EnrollmentStatus.Match,
            $"Expected the mistyped DOB 2008-08-04 alone to NOT match (the failure DC-500 fixes), " +
            $"but it matched. {Describe(rowsForChild, result)}");
    }

    private static ChildCheckRequest MakeChild(DateOnly dob) =>
        new()
        {
            CheckId = Guid.NewGuid(),
            FirstName = FirstName,
            LastName = LastName,
            DateOfBirth = dob
        };

    private static Dictionary<string, ChildCheckRequest> IndexMap(ChildCheckRequest child) =>
        new(StringComparer.Ordinal) { ["1"] = child };

    /// <summary>Rethrows on 401 (auth misconfig); otherwise fails with the CBMS error detail.</summary>
    private static void FailOnError(ErrorResponse ex, string payloadDescription)
    {
        if (ex.ResponseStatusCode == 401)
        {
            throw ex;
        }

        var details = ex.ErrorDetails?
            .Select(d => $"{d.Code ?? "(no code)"}: {d.Message ?? "(no message)"}")
            .ToArray() ?? [];
        Assert.Fail(
            $"CBMS UAT rejected the {payloadDescription} check-enrollment payload. " +
            "Either the payload shape is invalid or Devora Robert is not an enrollment in this UAT environment. " +
            $"Status={ex.ResponseStatusCode}, Message={ex.Message}, ErrorDetails=[{string.Join("; ", details)}]");
    }

    /// <summary>Summarizes the best CBMS row and the correlated result for assertion messages.</summary>
    private static string Describe(IReadOnlyList<CheckEnrollmentStudentDetail> rowsForChild, ChildCheckResult result)
    {
        var matchedRow = rowsForChild
            .OrderByDescending(r => r.MtchCnfd ?? double.NegativeInfinity)
            .FirstOrDefault();

        string rowSummary;
        if (matchedRow is null)
        {
            rowSummary = "no rows under stdReqInd \"1\"";
        }
        else
        {
            var eligSts = matchedRow.AdditionalData.TryGetValue("sebtEligSts", out var elig)
                ? elig?.ToString()
                : null;
            rowSummary = $"stdDob={matchedRow.StdDob}, mtchCnfd={matchedRow.MtchCnfd}, sebtEligSts={eligSts ?? "(none)"}";
        }

        return $"CBMS best row: {rowSummary}. Correlated result: DOB={result.DateOfBirth:yyyy-MM-dd}, " +
               $"status={result.Status}, confidence={result.MatchConfidence}.";
    }
}
