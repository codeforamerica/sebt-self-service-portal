using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// An IAL requirement that is either uniform (same level for all case types)
/// or per-case-type (different levels depending on how cases were loaded).
/// </summary>
public class IalRequirement
{
    private readonly IalLevel? _uniform;
    private readonly Dictionary<string, IalLevel>? _perCaseType;

    private IalRequirement(IalLevel? uniform, Dictionary<string, IalLevel>? perCaseType)
    {
        _uniform = uniform;
        _perCaseType = perCaseType;
    }

    /// <summary>Creates a requirement with the same level for all case types.</summary>
    public static IalRequirement Uniform(IalLevel level) => new(level, null);

    /// <summary>Creates a requirement with per-case-type levels. "Highest wins" on resolve.</summary>
    public static IalRequirement PerCaseType(Dictionary<string, IalLevel> levels) => new(null, levels);

    /// <summary>Creates a default requirement of IAL1plus (fail-safe).</summary>
    public static IalRequirement Default() => Uniform(IalLevel.IAL1plus);

    /// <summary>
    /// Resolves the required IAL level. For uniform requirements, returns the
    /// level directly. For per-case-type, applies "highest wins" across cases.
    /// Returns <see cref="UserIalLevel.IAL1"/> when no cases are provided.
    /// </summary>
    public UserIalLevel Resolve(IReadOnlyList<SummerEbtCase> cases)
    {
        if (_uniform.HasValue)
        {
            return ToUserIalLevel(_uniform.Value);
        }

        // No cases = no case-derived reason to require elevated IAL.
        // Uniform requirements on the same resource (e.g., address+view) still apply independently.
        if (_perCaseType is null || cases.Count == 0)
        {
            return UserIalLevel.IAL1;
        }

        var highest = cases.Max(c => ClassifyCase(c, _perCaseType));
        return ToUserIalLevel(highest);
    }

    /// <summary>Returns all configured IAL levels (for validation comparisons).</summary>
    public IEnumerable<IalLevel> AllLevels()
    {
        if (_uniform.HasValue)
        {
            return [_uniform.Value];
        }

        return _perCaseType?.Values ?? Enumerable.Empty<IalLevel>();
    }

    public override string ToString()
    {
        if (_uniform.HasValue)
        {
            return _uniform.Value.ToString();
        }
        else if (_perCaseType is not null)
        {
            return $"[{string.Join(',', _perCaseType.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}]";
        }
        return "unknown";
    }

    // Case-type key lookup is case-insensitive when the dictionary is created with
    // StringComparer.OrdinalIgnoreCase (see ConfigureIdProofingRequirements).
    // If a case type is not in the dictionary, fail-closed to IAL1plus.
    private static IalLevel ClassifyCase(SummerEbtCase c, Dictionary<string, IalLevel> levels)
    {
        string key;
        if (!c.IsStreamlineCertified)
        {
            key = "application";
        }
        else
        {
            key = c.IsCoLoaded ? "coloadedStreamline" : "streamline";
        }

        return levels.TryGetValue(key, out var level) ? level : IalLevel.IAL1plus;
    }

    private static UserIalLevel ToUserIalLevel(IalLevel level)
    {
        return level switch
        {
            IalLevel.IAL1 => UserIalLevel.IAL1,
            IalLevel.IAL1plus => UserIalLevel.IAL1plus,
            IalLevel.IAL2 => UserIalLevel.IAL2,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown IalLevel value")
        };
    }
}
