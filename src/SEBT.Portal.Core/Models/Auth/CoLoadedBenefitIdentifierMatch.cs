using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Verifies a co-loaded user's submitted SNAP/TANF identifier against values already on file
/// (user record and/or co-loaded Summer EBT cases from the state connector).
/// </summary>
public static class CoLoadedBenefitIdentifierMatch
{
    /// <summary>
    /// Returns true when <paramref name="idValue"/> matches on-file benefit identifiers for the given <paramref name="idType"/>.
    /// </summary>
    public static bool Matches(User user, HouseholdData? household, string idType, string idValue)
    {
        var normalized = Normalize(idValue);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        return idType switch
        {
            "snapAccountId" => MatchSnapAccount(user, household, normalized),
            "snapPersonId" => MatchSnapPerson(household, normalized),
            "tanfAccountId" => MatchTanfAccount(user, household, normalized),
            "tanfPersonId" => MatchTanfPerson(household, normalized),
            _ => false
        };
    }

    private static string Normalize(string value) => value.Trim();

    private static bool EqualsNormalized(string? onFile, string submitted) =>
        !string.IsNullOrWhiteSpace(onFile)
        && string.Equals(onFile.Trim(), submitted, StringComparison.OrdinalIgnoreCase);

    private static bool MatchSnapAccount(User user, HouseholdData? household, string v) =>
        EqualsNormalized(user.SnapId, v)
        || AnyMatchingCoLoadedCase(household, IsSnapCase, c =>
            EqualsNormalized(c.EbtCaseNumber, v)
            || EqualsNormalized(c.CaseDisplayNumber, v)
            || EqualsNormalized(c.SummerEBTCaseID, v));

    private static bool MatchSnapPerson(HouseholdData? household, string v) =>
        AnyMatchingCoLoadedCase(household, IsSnapCase, c => EqualsNormalized(c.ApplicationStudentId, v));

    private static bool MatchTanfAccount(User user, HouseholdData? household, string v) =>
        EqualsNormalized(user.TanfId, v)
        || AnyMatchingCoLoadedCase(household, IsTanfCase, c =>
            EqualsNormalized(c.EbtCaseNumber, v)
            || EqualsNormalized(c.CaseDisplayNumber, v)
            || EqualsNormalized(c.SummerEBTCaseID, v));

    private static bool MatchTanfPerson(HouseholdData? household, string v) =>
        AnyMatchingCoLoadedCase(household, IsTanfCase, c => EqualsNormalized(c.ApplicationStudentId, v));

    private static bool IsSnapCase(SummerEbtCase c) =>
        c.IssuanceType == IssuanceType.SnapEbtCard
        || string.Equals(c.EligibilityType, "SNAP", StringComparison.OrdinalIgnoreCase);

    private static bool IsTanfCase(SummerEbtCase c) =>
        c.IssuanceType == IssuanceType.TanfEbtCard
        || string.Equals(c.EligibilityType, "TANF", StringComparison.OrdinalIgnoreCase);

    private static bool AnyMatchingCoLoadedCase(
        HouseholdData? household,
        Func<SummerEbtCase, bool> typeFilter,
        Func<SummerEbtCase, bool> valueMatches)
    {
        if (household?.SummerEbtCases is not { Count: > 0 })
        {
            return false;
        }

        foreach (var c in household.SummerEbtCases)
        {
            if (!c.IsCoLoaded || !typeFilter(c))
            {
                continue;
            }

            if (valueMatches(c))
            {
                return true;
            }
        }

        return false;
    }
}
