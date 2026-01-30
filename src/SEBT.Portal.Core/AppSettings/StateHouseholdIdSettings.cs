using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for state-specific preferred household ID types used to authorize guardians and link to household data.
/// When the multi-state plugin is available, it may own this configuration; until then, appsettings or env provide it.
/// </summary>
public class StateHouseholdIdSettings
{
    public static readonly string SectionName = "StateHouseholdId";

    /// <summary>
    /// The current state code (e.g. "dc", "co"). Used to select which state's preferred ID types apply.
    /// Can be overridden by environment variable STATE or request header when the multi-state plugin is in use.
    /// </summary>
    public string CurrentState { get; set; } = "dc";

    /// <summary>
    /// Per-state configuration. Key is state code (e.g. "dc", "co"); value is that state's preferred household ID settings.
    /// </summary>
    public Dictionary<string, StatePreferredHouseholdIdEntry> States { get; set; } = new();
}

/// <summary>
/// Preferred household ID type(s) for a single state. A state may support one or more types (e.g. Email now, SNAP ID later).
/// </summary>
public class StatePreferredHouseholdIdEntry
{
    /// <summary>
    /// Ordered list of household ID types this state uses for authorization/linking. The first type that can be resolved from the user is used for lookup.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one preferred household ID type is required per state.")]
    public List<PreferredHouseholdIdType> PreferredHouseholdIdTypes { get; set; } = [PreferredHouseholdIdType.Email];
}
