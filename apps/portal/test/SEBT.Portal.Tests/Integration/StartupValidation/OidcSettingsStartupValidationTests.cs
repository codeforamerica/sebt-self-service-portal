using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves an incomplete OIDC client stops the app at startup rather than at login.
/// A gap in these values leaves the authorize redirect working and fails only once the
/// visitor returns from the IdP, so the boot is the last place to catch it cheaply.
/// </summary>
/// <remarks>
/// The base fixture configures a complete OIDC client, so each test blanks exactly one
/// value. Clearing the discovery endpoint is how a deployment says "no OIDC" — those
/// instances must still boot with the rest of the section empty.
/// </remarks>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OidcSettingsStartupValidationTests : StartupValidationTestBase
{
    [Fact]
    public void Startup_WithTooShortOidcSigningKey_ThrowsOptionsValidationException()
    {
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey", "too-short");
        using var factory = CreateFactory();

        var ex = Assert.Throws<OptionsValidationException>(factory.CreateClient);
        Assert.Contains("CompleteLoginSigningKey", ex.Message);
    }

    [Fact]
    public void Startup_WithOidcEnabledAndEmptySigningKey_ThrowsOptionsValidationException()
    {
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey", "");
        using var factory = CreateFactory();

        var ex = Assert.Throws<OptionsValidationException>(factory.CreateClient);
        Assert.Contains("CompleteLoginSigningKey", ex.Message);
    }

    [Fact]
    public void Startup_WithOidcEnabledAndNoClientId_ThrowsOptionsValidationException()
    {
        Environment.SetEnvironmentVariable("Oidc__ClientId", "");
        using var factory = CreateFactory();

        var ex = Assert.Throws<OptionsValidationException>(factory.CreateClient);
        Assert.Contains("ClientId", ex.Message);
    }

    [Fact]
    public void Startup_WithOidcEnabledAndNoCallbackRedirectUri_ThrowsOptionsValidationException()
    {
        Environment.SetEnvironmentVariable("Oidc__CallbackRedirectUri", "");
        using var factory = CreateFactory();

        var ex = Assert.Throws<OptionsValidationException>(factory.CreateClient);
        Assert.Contains("CallbackRedirectUri", ex.Message);
    }

    [Fact]
    public void Startup_WithoutOidc_AllowsAnEmptySection()
    {
        // A state that never uses OIDC leaves the whole client unset, including the signing
        // key the base appsettings ships. That must not block its boot.
        Environment.SetEnvironmentVariable("Oidc__DiscoveryEndpoint", "");
        Environment.SetEnvironmentVariable("Oidc__ClientId", "");
        Environment.SetEnvironmentVariable("Oidc__CallbackRedirectUri", "");
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey", "");
        using var factory = CreateFactory();

        factory.CreateClient();
    }
}
