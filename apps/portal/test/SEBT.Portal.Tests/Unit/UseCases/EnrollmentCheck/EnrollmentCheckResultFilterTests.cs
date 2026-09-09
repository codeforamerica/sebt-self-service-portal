using SEBT.Portal.Core.StateConnector;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.UseCases.EnrollmentCheck;

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

    [Fact]
    public void Filter_WhenDobMatchesExactly_KeepsResult()
    {
        var request = MakeRequest(firstName: "Janet", lastName: "Smith");
        var result = MakeResult(firstName: "Janet", lastName: "Smith");

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_WhenFullNameMatchesCaseInsensitively_KeepsResult()
    {
        // Names match (case-insensitive), birth year matches, but month/day differ — name combo + year is enough
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2000, 6, 6));
        var result = MakeResult(firstName: "jane", lastName: "doe", dob: new DateOnly(2000, 1, 1));

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_WhenNameMatchesButBirthYearDiffers_DropsResult()
    {
        // Full name matches but birth year is wrong — year is always required
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2000, 6, 6));
        var result = MakeResult(firstName: "Jane", lastName: "Doe", dob: new DateOnly(1999, 6, 6));

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_WhenDobMatchesButNamesAreDifferent_KeepsResult()
    {
        // DOB matches (default on both), names differ — DOB alone is enough
        var request = MakeRequest(firstName: "Jane", lastName: "Doe");
        var result = MakeResult(firstName: "Janet", lastName: "Smith");

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_WhenNeitherDobNorNameMatches_DropsResult()
    {
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2015, 3, 12));
        var result = MakeResult(firstName: "Robert", lastName: "Smith", dob: new DateOnly(2010, 6, 1));

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_WhenOnlyFirstNameMatches_DropsResult()
    {
        // First name matches, last name does not, DOB does not — full combo required
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2015, 3, 12));
        var result = MakeResult(firstName: "Jane", lastName: "Smith", dob: new DateOnly(2010, 6, 1));

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_WhenOnlyLastNameMatches_DropsResult()
    {
        // Last name matches, first name does not, DOB does not — full combo required
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2015, 3, 12));
        var result = MakeResult(firstName: "Robert", lastName: "Doe", dob: new DateOnly(2010, 6, 1));

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_WhenResultStatusIsError_KeepsRegardlessOfMatch()
    {
        // Errors aren't candidates; don't filter them
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2015, 3, 12));
        var result = MakeResult(firstName: "Robert", lastName: "Smith", dob: new DateOnly(2010, 6, 1),
            status: EnrollmentStatus.Error);

        var filtered = EnrollmentCheckResultFilter.Filter([request], [result]);

        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_WhenResultStatusIsNonMatch_KeepsRegardlessOfMatch()
    {
        // NonMatch results are already non-matches; no need to filter further
        var request = MakeRequest(firstName: "Jane", lastName: "Doe", dob: new DateOnly(2015, 3, 12));
        var result = MakeResult(firstName: "Robert", lastName: "Smith", dob: new DateOnly(2010, 6, 1),
            status: EnrollmentStatus.NonMatch);

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
