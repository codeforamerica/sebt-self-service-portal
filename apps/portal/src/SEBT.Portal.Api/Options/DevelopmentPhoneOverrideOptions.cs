namespace SEBT.Portal.Api.Options;

/// <summary>
/// Development-only configuration to override the phone used for household lookup.
/// When set, takes precedence over the phone in the JWT or user record.
/// </summary>
public class DevelopmentPhoneOverrideOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "DevelopmentPhoneOverride";

    /// <summary>
    /// Phone number to use for household lookup instead of the JWT value.
    /// Set in appsettings.Development.json or state settings file
    /// </summary>
    public string Phone { get; set; } = string.Empty;
}
