namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Declares WHICH source record fields compose a case's opaque, self-describing caseId token
/// (DC-568 spike). This is NOT a config DSL: the pack/unpack mechanism (named fields → small JSON
/// object → URL-safe base64) is a fixed platform primitive in Infrastructure. Config only lists
/// the routing fields a later write needs to route the call.
/// </summary>
/// <remarks>
/// Read side: the response mapper reads each named source field off the record and packs it under
/// its <see cref="Fields"/> KEY (OUR routing-field name) into the token.
/// Write side: the driver decodes the token back into that same OUR-keyed field set and exposes
/// those fields as inputs to the request binding.
/// </remarks>
public sealed record CaseIdComposition
{
    /// <summary>
    /// OUR routing-field name → the source property on the record whose value it carries. The
    /// LHS keys are what the write-side request binding refers to; the RHS is the raw backend
    /// property to read on the read.
    /// </summary>
    public required Dictionary<string, string> Fields { get; init; }
}
