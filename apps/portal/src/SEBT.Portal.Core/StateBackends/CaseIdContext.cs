namespace SEBT.Portal.Core.StateBackends;

/// <summary>Caller-context values a caseId composition's <c>fromContext</c> can pack — closed vocabulary; a new value means a new property here plus its resolution in code.</summary>
public sealed record CaseIdContext
{
    /// <summary>The identifier the lookup searched by; the composition packs empty when null.</summary>
    public string? HouseholdIdentifier { get; init; }
}
