using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for preferred household ID types used to authorize guardians and link to household data.
/// The first type that can be resolved from the user is used for lookup.
/// </summary>
public class StateHouseholdIdSettings : IHaveConfigSectionName
{
    public static string SectionName => "StateHouseholdId";

    /// <summary>
    /// Ordered list of household ID types for authorization/linking.
    /// The first type that can be resolved from the user is used for lookup.
    /// </summary>
    [MinLength(1, ErrorMessage = "StateHouseholdId:PreferredHouseholdIdTypes must list at least one type; with none, no guardian can be linked to a household.")]
    public HashSet<PreferredHouseholdIdType> PreferredHouseholdIdTypes { get; set; } = [];
}
