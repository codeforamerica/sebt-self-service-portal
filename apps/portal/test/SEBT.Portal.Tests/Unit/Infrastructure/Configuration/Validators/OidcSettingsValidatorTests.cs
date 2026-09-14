using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class OidcSettingsValidatorTests
{
    private const string Discovery = "https://example.com/.well-known/openid-configuration";
    private static readonly string ValidKey = new('x', 32);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenOidcIsOff_PassesRegardlessOfOtherValues(string? discovery)
    {
        // An absent discovery endpoint is how a deployment says "no OIDC" — the state
        // allowlist reads it the same way — so nothing else is required of it.
        var validator = new OidcSettingsValidator();
        var settings = new OidcSettings { DiscoveryEndpoint = discovery };

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenFullyConfigured_Passes()
    {
        var validator = new OidcSettingsValidator();

        var result = validator.Validate(name: null, options: CreateEnabledSettings());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenClientIdMissing_Fails(string? clientId)
    {
        var validator = new OidcSettingsValidator();
        var settings = CreateEnabledSettings();
        settings.ClientId = clientId;

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Failed);
        Assert.Contains("Oidc:ClientId", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenCallbackRedirectUriMissing_Fails(string? callbackRedirectUri)
    {
        var validator = new OidcSettingsValidator();
        var settings = CreateEnabledSettings();
        settings.CallbackRedirectUri = callbackRedirectUri;

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Failed);
        Assert.Contains("Oidc:CallbackRedirectUri", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_WhenSigningKeyMissing_Fails(string? key)
    {
        // The key signs the complete-login handoff. Without it that step fails after the
        // user has already returned from the IdP, which is the failure this validator moves
        // forward to startup.
        var validator = new OidcSettingsValidator();
        var settings = CreateEnabledSettings();
        settings.CompleteLoginSigningKey = key;

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Failed);
        Assert.Contains("Oidc:CompleteLoginSigningKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReportsEveryMissingKeyAtOnce()
    {
        // One boot should tell an operator everything to fix, not surface the next gap only
        // after the first is corrected.
        var validator = new OidcSettingsValidator();
        var settings = new OidcSettings { DiscoveryEndpoint = Discovery };

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Failed);
        Assert.Contains("Oidc:ClientId", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Oidc:CallbackRedirectUri", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Oidc:CompleteLoginSigningKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Validate_FailsForTooShortSigningKey(int charCount)
    {
        var validator = new OidcSettingsValidator();
        var settings = CreateEnabledSettings();
        settings.CompleteLoginSigningKey = new string('x', charCount);

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Failed);
        Assert.Contains("32 characters", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void Validate_PassesFor32CharOrGreaterKey(int charCount)
    {
        var validator = new OidcSettingsValidator();
        var settings = CreateEnabledSettings();
        settings.CompleteLoginSigningKey = new string('x', charCount);

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOidcIsOff_IgnoresAShortSigningKey()
    {
        // The base appsettings ships a signing key with no discovery endpoint; that is not a
        // configured OIDC tenant and must not block startup for states that never use OIDC.
        var validator = new OidcSettingsValidator();
        var settings = new OidcSettings { CompleteLoginSigningKey = "short" };

        var result = validator.Validate(name: null, options: settings);

        Assert.True(result.Succeeded);
    }

    /// <summary>A complete, OIDC-enabled configuration; tests blank one field at a time.</summary>
    private static OidcSettings CreateEnabledSettings() => new()
    {
        DiscoveryEndpoint = Discovery,
        ClientId = "TEST_CLIENT_ID",
        ClientSecret = "TEST_CLIENT_SECRET",
        CallbackRedirectUri = "https://example.com/oidc-callback",
        CompleteLoginSigningKey = ValidKey
    };
}
