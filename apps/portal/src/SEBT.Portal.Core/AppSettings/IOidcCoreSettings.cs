namespace SEBT.Portal.Core.AppSettings;

public interface IOidcCoreSettings
{
    string? DiscoveryEndpoint { get; }

    string? ClientId { get; }

    string? ClientSecret { get; }

    string? RedirectUri { get; }
}
