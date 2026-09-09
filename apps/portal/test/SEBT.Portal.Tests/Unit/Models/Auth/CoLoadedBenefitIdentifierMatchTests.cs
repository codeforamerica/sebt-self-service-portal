using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models.Auth;

public class CoLoadedBenefitIdentifierMatchTests
{
    [Fact]
    public void Matches_ReturnsTrue_WhenSnapAccountEqualsUserSnapId()
    {
        var user = new User { SnapId = "SNAP-CO-001" };
        Assert.True(CoLoadedBenefitIdentifierMatch.Matches(user, null, "snapAccountId", "snap-co-001"));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenSnapAccountEqualsCoLoadedCaseEbtNumber()
    {
        var user = new User();
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    IsCoLoaded = true,
                    IssuanceType = IssuanceType.SnapEbtCard,
                    EbtCaseNumber = "ACC-99"
                }
            ]
        };

        Assert.True(CoLoadedBenefitIdentifierMatch.Matches(user, household, "snapAccountId", "acc-99"));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenSnapAccountEqualsCoLoadedCaseDisplayNumber()
    {
        var user = new User();
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    IsCoLoaded = true,
                    IssuanceType = IssuanceType.SnapEbtCard,
                    EbtCaseNumber = "CBMS-INTERNAL",
                    CaseDisplayNumber = "APP-777"
                }
            ]
        };

        Assert.True(CoLoadedBenefitIdentifierMatch.Matches(user, household, "snapAccountId", "app-777"));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenSnapPersonEqualsApplicationStudentId()
    {
        var user = new User();
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    IsCoLoaded = true,
                    EligibilityType = "SNAP",
                    ApplicationStudentId = "P-1"
                }
            ]
        };

        Assert.True(CoLoadedBenefitIdentifierMatch.Matches(user, household, "snapPersonId", "P-1"));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenCaseNotCoLoaded()
    {
        var user = new User();
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    IsCoLoaded = false,
                    IssuanceType = IssuanceType.SnapEbtCard,
                    EbtCaseNumber = "X"
                }
            ]
        };

        Assert.False(CoLoadedBenefitIdentifierMatch.Matches(user, household, "snapAccountId", "X"));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenTanfAccountEqualsUserTanfId()
    {
        var user = new User { TanfId = "TANF-1" };
        Assert.True(CoLoadedBenefitIdentifierMatch.Matches(user, null, "tanfAccountId", "tanf-1"));
    }
}
