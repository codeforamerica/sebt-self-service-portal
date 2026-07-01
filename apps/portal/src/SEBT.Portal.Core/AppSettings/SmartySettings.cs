using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for Smarty US Street Address API.
/// </summary>
public sealed class SmartySettings
{
    public const string SectionName = "Smarty";

    /// <summary>
    /// When false, the portal uses pass-through normalization only (no HTTP calls to Smarty).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Smarty embedded key auth-id.</summary>
    public string? AuthId { get; set; }

    /// <summary>Smarty embedded key auth-token (secret).</summary>
    public string? AuthToken { get; set; }

    /// <summary>API host without trailing slash (e.g. https://us-street.api.smartystreets.com).</summary>
    public string BaseUrl { get; set; } = "https://us-street.api.smartystreets.com";

    /// <summary>HTTP timeout for Smarty requests.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 20;
}
