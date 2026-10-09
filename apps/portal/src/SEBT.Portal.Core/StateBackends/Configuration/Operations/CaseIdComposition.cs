namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>Which fields compose a case's opaque (not encrypted) caseId token; a later write decodes them back as request-binding inputs.</summary>
public sealed record CaseIdComposition
{
    /// <summary>Our routing-field name → the source property on the record whose value it carries.</summary>
    public required Dictionary<string, string> Fields { get; init; }

    /// <summary>Our routing-field name → a named <c>CaseIdContext</c> value; context names are a closed vocabulary resolved in code.</summary>
    public Dictionary<string, string>? FromContext { get; init; }
}
