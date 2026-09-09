namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Whether the guardian has qualifying portal household data before sending them
/// through Socure. Aligns with the dashboard empty-state check (no enrolled cases and
/// no applications). A missing household is treated like an empty household.
/// </summary>
public static class HouseholdSocureEligibility
{
    /// <summary>
    /// True when there is at least one summer EBT case or one household-level application.
    /// </summary>
    public static bool HasQualifyingHouseholdForSocure(HouseholdData? household)
    {
        if (household == null)
        {
            return false;
        }

        return household.SummerEbtCases.Count > 0 || household.Applications.Count > 0;
    }
}
