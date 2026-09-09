using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Api.Services;

/// <summary>
/// Always returns null — no phone override is applied.
/// </summary>
public class NullPhoneOverrideProvider : IPhoneOverrideProvider
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullPhoneOverrideProvider Instance = new();

    /// <inheritdoc />
    public string? GetOverridePhone() => null;
}
