namespace SEBT.Portal.Core.AppSettings;

public class OidcSettings : IOidcCoreSettings, IHaveConfigSectionName
{
    public static string SectionName => "Oidc";
    
    public string? DiscoveryEndpoint { get; set; }
    
    public string? ClientId { get; set; }
    
    public string? ClientSecret { get; set; }
    
    public string? CallbackRedirectUri { get; set; }
    
    public string? CompleteLoginSigningKey { get; set; }
    
    // Deployment-specific JWT issuer/audience; symmetric on sign + validate.
    public string PortalOrigin => CallbackRedirectUri?.TrimEnd('/') ?? "sebt-portal";
}
