using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for preferred household ID types used to authorize guardians and link to household data.
/// The first type that can be resolved from the user is used for lookup.
/// </summary>
public class StateHouseholdIdSettings
{
    public static readonly string SectionName = "StateHouseholdId";

    /// <summary>
    /// Ordered list of household ID types for authorization/linking.
    /// The first type that can be resolved from the user is used for lookup.
    /// </summary>
    public List<PreferredHouseholdIdType> PreferredHouseholdIdTypes { get; set; } = [PreferredHouseholdIdType.Email];
}
