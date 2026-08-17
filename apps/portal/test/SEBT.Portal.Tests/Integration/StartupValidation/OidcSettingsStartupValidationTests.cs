using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves the app fails to start when Oidc:CompleteLoginSigningKey is present
/// but less than 32 chars.
/// </summary>
/// <remarks>Empty/absent is deliberately allowed for states without OIDC.</remarks>
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
    public void Startup_WithEmptyOidcSigningKey_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey", "");
        using var factory = CreateFactory();

        factory.CreateClient(); // empty is allowed → boot succeeds
    }
}
