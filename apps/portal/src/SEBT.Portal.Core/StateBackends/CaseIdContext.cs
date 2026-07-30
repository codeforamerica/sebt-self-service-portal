namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Caller-context values from a household lookup that a caseId composition can pack via its
/// <c>fromContext</c> entries — for routing identifiers a write needs but the lookup response
/// never echoes. The names a config may reference form a closed vocabulary resolved in fixed
/// code; today only <c>householdIdentifier</c>.
/// </summary>
public sealed record CaseIdContext
{
    /// <summary>
    /// The identifier value the lookup searched by (context name <c>householdIdentifier</c>).
    /// Null when the caller has no single household identifier; the composition then packs empty.
    /// </summary>
    public string? HouseholdIdentifier { get; init; }
}
