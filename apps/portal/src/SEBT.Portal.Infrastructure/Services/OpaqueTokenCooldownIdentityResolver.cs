using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Resolves the cooldown identity for case IDs served as <see cref="OpaqueCaseId"/>
/// tokens: decodes the token and returns its <c>caseId</c> routing field — the raw
/// state case ID that cooldown rows have always been hashed from.
/// </summary>
/// <remarks>
/// A value that does not decode as a token is returned unchanged: it is already a
/// raw state case ID (a client mid-session across a cutover, or mock data), and
/// raw IDs are the canonical identity. A raw ID that happens to decode as a
/// base64url JSON string dictionary would be misread as a token, but state case
/// IDs are short numeric/dashed strings for which that cannot occur.
/// </remarks>
public class OpaqueTokenCooldownIdentityResolver : ICooldownIdentityResolver
{
    /// <inheritdoc />
    public string ResolveCanonicalCaseIdentity(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);

        IReadOnlyDictionary<string, string> routingFields;
        try
        {
            routingFields = OpaqueCaseId.Decode(caseId);
        }
        catch (InvalidOperationException)
        {
            // Not a token — the value is already the canonical raw case ID.
            return caseId;
        }

        if (!routingFields.TryGetValue("caseId", out var rawCaseId) || string.IsNullOrEmpty(rawCaseId))
        {
            throw new InvalidOperationException(
                "Opaque case token decoded without a caseId routing field; cannot resolve a cooldown identity.");
        }

        return rawCaseId;
    }
}
