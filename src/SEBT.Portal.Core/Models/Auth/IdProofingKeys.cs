namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Maps <see cref="ProtectedResource"/>+<see cref="ProtectedAction"/> enum pairs
/// to the config key strings used in IdProofingRequirements.
/// </summary>
public static class IdProofingKeys
{
    public const string AddressView = "address+view";
    public const string AddressWrite = "address+write";
    public const string EmailView = "email+view";
    public const string PhoneView = "phone+view";
    public const string HouseholdView = "household+view";
    public const string CardWrite = "card+write";

    /// <summary>
    /// Converts a resource+action enum pair to its config key string.
    /// </summary>
    public static string ToConfigKey(ProtectedResource resource, ProtectedAction action)
        => $"{resource.ToString().ToLowerInvariant()}+{action.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Returns all valid config key strings, derived from the enum values.
    /// Used by the config binder to warn on unrecognized keys.
    /// </summary>
    public static IReadOnlySet<string> AllValidKeys { get; } = BuildAllValidKeys();

    private static HashSet<string> BuildAllValidKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in Enum.GetValues<ProtectedResource>())
            foreach (var action in Enum.GetValues<ProtectedAction>())
                keys.Add(ToConfigKey(resource, action));
        return keys;
    }
}
