namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Which source record fields compose a case's caseId token — opaque, not encrypted. The read packs
/// these fields into the token; a later write decodes them back as inputs to its request binding.
/// </summary>
public sealed record CaseIdComposition
{
    /// <summary>Our routing-field name → the source property on the record whose value it carries.</summary>
    public required Dictionary<string, string> Fields { get; init; }

    /// <summary>
    /// Our routing-field name → a named caller-context value, for routing identifiers the lookup
    /// response never echoes (e.g. the identifier the portal searched with). Context names are a
    /// closed vocabulary resolved in fixed code — today only <c>householdIdentifier</c>.
    /// </summary>
    public Dictionary<string, string>? FromContext { get; init; }
}
