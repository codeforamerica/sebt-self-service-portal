using System.Text;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Unified settings for all identity proofing requirements, keyed by
/// resource+action (e.g. "address+view", "card+write").
/// See docs/config/ial/README.md for the configuration guide.
/// </summary>
public class IdProofingRequirementsSettings
{
    public static readonly string SectionName = "IdProofingRequirements";

    /// <summary>
    /// Map of config key (e.g. "address+view") to its IAL requirement.
    /// Populated by <c>ConfigureIdProofingRequirements</c>.
    /// </summary>
    public Dictionary<string, IalRequirement> Requirements { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the requirement for a config key. Returns <see cref="IalRequirement.Default"/>
    /// (IAL1plus) if the key is not configured — fail-safe by design.
    /// </summary>
    public IalRequirement Get(string key)
    {
        return Requirements.TryGetValue(key, out var req) ? req : IalRequirement.Default();
    }

    /// <summary>
    /// Gets the requirement for a resource+action enum pair.
    /// </summary>
    public IalRequirement Get(ProtectedResource resource, ProtectedAction action)
    {
        return Get(IdProofingKeys.ToConfigKey(resource, action));
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");

        foreach (var kvp in Requirements)
        {
            sb.AppendLine($"    {kvp.Key}: {kvp.Value},");
        }

        sb.AppendLine("]");

        return sb.ToString();
    }
}
