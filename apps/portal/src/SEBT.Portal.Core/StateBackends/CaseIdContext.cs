namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Caller-context values a caseId composition's <c>fromContext</c> entries can pack. Context names
/// are a closed vocabulary resolved in fixed code; today only <c>householdIdentifier</c>.
/// </summary>
public sealed record CaseIdContext
{
    /// <summary>The identifier the lookup searched by; the composition packs empty when null.</summary>
    public string? HouseholdIdentifier { get; init; }
}
