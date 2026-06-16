using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models.Household;

public class CoLoadedCohortClassifierTests
{
    [Fact]
    public void Classify_ReturnsNonCoLoaded_WhenHouseholdIsNull()
    {
        Assert.Equal(CoLoadedCohort.NonCoLoaded, CoLoadedCohortClassifier.Classify(null));
    }

    [Fact]
    public void Classify_ReturnsCoLoadedOnly_WhenAllCasesAreCoLoadedAndNoApplications()
    {
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    SummerEBTCaseID = "S1",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    IsCoLoaded = true
                }
            ]
        };

        Assert.Equal(CoLoadedCohort.CoLoadedOnly, CoLoadedCohortClassifier.Classify(household));
    }

    [Fact]
    public void Classify_ReturnsMixedOrApplicantExcluded_WhenNonCoLoadedCaseExists()
    {
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    SummerEBTCaseID = "S1",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    IsCoLoaded = true
                },
                new SummerEbtCase
                {
                    SummerEBTCaseID = "S2",
                    ChildFirstName = "C",
                    ChildLastName = "D",
                    IsCoLoaded = false
                }
            ]
        };

        Assert.Equal(CoLoadedCohort.MixedOrApplicantExcluded, CoLoadedCohortClassifier.Classify(household));
    }

    [Fact]
    public void ResolveOffboardingReason_ReturnsCoLoadedOnly_WhenHouseholdIsCoLoadedOnly()
    {
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    SummerEBTCaseID = "S1",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    IsCoLoaded = true
                }
            ]
        };

        Assert.Equal("coLoadedOnly", CoLoadedCohortClassifier.ResolveOffboardingReason("idProofingFailed", household));
    }

    [Fact]
    public void ResolveOffboardingReason_ReturnsDefaultReason_WhenHouseholdIsNotCoLoadedOnly()
    {
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    SummerEBTCaseID = "S1",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    IsCoLoaded = false
                }
            ]
        };

        Assert.Equal("idProofingFailed", CoLoadedCohortClassifier.ResolveOffboardingReason("idProofingFailed", household));
    }
}
