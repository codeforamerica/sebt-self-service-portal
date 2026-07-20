namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// OIDC client used for elevated verification (IAL1+ step-up). Public values only; secrets belong on the Next.js server.
/// Binds to <c>Oidc:StepUp</c> in configuration.
/// </summary>
public class OidcStepUpSettings
{
    public const string SectionName = "Oidc:StepUp";

    /// <summary>OpenID Connect discovery document URL.</summary>
    public string? DiscoveryEndpoint { get; set; }

    /// <summary>OAuth2 client id registered with the IdP for step-up.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional redirect URI; when unset, <c>Oidc:CallbackRedirectUri</c> is used.</summary>
    public string? RedirectUri { get; set; }
}
