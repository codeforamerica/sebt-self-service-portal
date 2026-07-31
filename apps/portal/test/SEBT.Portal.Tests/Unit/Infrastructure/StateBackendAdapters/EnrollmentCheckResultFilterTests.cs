using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackendAdapters;

public class EnrollmentCheckResultFilterTests
{
    private static readonly Guid CheckId = Guid.NewGuid();

    private static ChildCheckRequest MakeRequest(
        string firstName = "Jane",
        string lastName = "Doe",
        DateOnly? dob = null) =>
        new()
        {
            CheckId = CheckId,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dob ?? new DateOnly(2015, 3, 12)
        };

    private static ChildCheckResult MakeResult(
        string firstName = "Jane",
        string lastName = "Doe",
        DateOnly? dob = null,
        EnrollmentStatus status = EnrollmentStatus.Match) =>
        new()
        {
            CheckId = CheckId,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dob ?? new DateOnly(2015, 3, 12),
            Status = status
        };

    // The guard rule over a request for Jane Doe born on the given date: the birth year must
    // always match, and then either the full DOB or the full name (case-insensitive) exactly.
    [Theory]
    // Everything matches → kept.
    [InlineData("2015-03-12", "Jane", "Doe", "2015-03-12", true)]
    // DOB matches, names differ → DOB alone is enough.
    [InlineData("2015-03-12", "Janet", "Smith", "2015-03-12", true)]
    // Name matches case-insensitively + year matches (month/day differ) → name combo + year is enough.
    [InlineData("2000-06-06", "jane", "doe", "2000-01-01", true)]
    // Full name matches but birth year is wrong — year is always required.
    [InlineData("2000-06-06", "Jane", "Doe", "1999-06-06", false)]
    // Neither DOB nor name matches.
    [InlineData("2015-03-12", "Robert", "Smith", "2010-06-01", false)]
    // First name alone is not enough.
    [InlineData("2015-03-12", "Jane", "Smith", "2010-06-01", false)]
    // Last name alone is not enough.
    [InlineData("2015-03-12", "Robert", "Doe", "2010-06-01", false)]
    public void Filter_KeepsMatchCandidate_OnlyWhenDobOrFullNameExactlyMatches(
        string requestDob, string resultFirstName, string resultLastName, string resultDob, bool expectedKept)
    {
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: DateOnly.Parse(requestDob));
        var result = MakeResult(
            firstName: resultFirstName, lastName: resultLastName, dob: DateOnly.Parse(resultDob));

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Equal(expectedKept ? 1 : 0, filtered.Count);
    }

    // Error and NonMatch results aren't match candidates; they pass through unfiltered.
    [Theory]
    [InlineData(EnrollmentStatus.Error)]
    [InlineData(EnrollmentStatus.NonMatch)]
    public void Filter_WhenResultStatusIsNotACandidate_KeepsRegardlessOfMatch(EnrollmentStatus status)
    {
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2015, 3, 12));
        var result = MakeResult(firstName: "Robert", lastName: "Smith", dob: new DateOnly(2010, 6, 1),
            status: status);

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_WithEmptyResults_ReturnsEmpty()
    {
        var filtered = EnrollmentCheckResultFilter.Filter([], []);

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_WithMixedResults_KeepsOnlyPassingOnes()
    {
        var matchingCheckId = Guid.NewGuid();
        var nonMatchingCheckId = Guid.NewGuid();
        var dob = new DateOnly(2015, 3, 12);

        var requests = new List<ChildCheckRequest>
        {
            new() { CheckId = matchingCheckId, FirstName = "Jane", LastName = "Doe", DateOfBirth = dob },
            new() { CheckId = nonMatchingCheckId, FirstName = "Jane", LastName = "Doe", DateOfBirth = dob }
        };
        var results = new List<ChildCheckResult>
        {
            // DOB matches → kept
            new() { CheckId = matchingCheckId, FirstName = "Janet", LastName = "Smith", DateOfBirth = dob, Status = EnrollmentStatus.Match },
            // Neither matches → dropped
            new() { CheckId = nonMatchingCheckId, FirstName = "Robert", LastName = "Jones", DateOfBirth = new DateOnly(2010, 1, 1), Status = EnrollmentStatus.PossibleMatch }
        };

        var filtered = EnrollmentCheckResultFilter.Filter(requests, results);

        Assert.Single(filtered);
        Assert.Equal(matchingCheckId, filtered[0].CheckId);
    }

    [Fact]
    public void Filter_WhenResultHasNoCorrespondingRequest_KeepsResult()
    {
        // Defensive: an unrecognized CheckId should not be silently dropped
        var result = MakeResult();

        var filtered = EnrollmentCheckResultFilter.Filter([], [result]);

        Assert.Single(filtered);
    }
}
