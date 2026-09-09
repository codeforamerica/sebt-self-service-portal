namespace SEBT.Portal.Core.Services;

/// <summary>
/// Provides an optional development-only phone override for household lookup.
/// When set, the resolver uses this phone instead of the one from the JWT/user record.
/// Only used in Development; implementations return null in Production.
/// </summary>
public interface IPhoneOverrideProvider
{
    /// <summary>
    /// Returns the override phone to use for household lookup, or null if no override is active.
    /// </summary>
    string? GetOverridePhone();
}
