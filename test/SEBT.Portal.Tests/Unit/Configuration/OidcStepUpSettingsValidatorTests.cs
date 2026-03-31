using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class OidcStepUpSettingsValidatorTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static OidcStepUpSettingsValidator CreateValidator(params (string Key, string? Value)[] config)
        => new(Configuration(config));

    [Fact]
    public void Validate_WhenNoStepUpValues_Succeeds()
    {
        var validator = CreateValidator(
            ("Oidc:DiscoveryEndpoint", "https://idp.example/.well-known/openid-configuration"),
            ("Oidc:ClientId", "client"),
            ("Oidc:CallbackRedirectUri", "http://localhost:3000/callback"));
        var settings = new OidcStepUpSettings();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOnlyDiscoverySet_Fails()
    {
        var validator = CreateValidator();
        var settings = new OidcStepUpSettings
        {
            DiscoveryEndpoint = "https://step-up.example/.well-known/openid-configuration"
        };

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        var message = string.Join(' ', result.Failures ?? []);
        Assert.Contains("Oidc:StepUp:ClientId", message, StringComparison.Ordinal);
        Assert.Contains("partially configured", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WhenOnlyClientIdSet_Fails()
    {
        var validator = CreateValidator();
        var settings = new OidcStepUpSettings { ClientId = "step-up-client" };

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("Oidc:StepUp:DiscoveryEndpoint", string.Join(' ', result.Failures ?? []), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenDiscoveryAndClientButNoRedirect_Fails()
    {
        var validator = CreateValidator();
        var settings = new OidcStepUpSettings
        {
            DiscoveryEndpoint = "https://step-up.example/.well-known/openid-configuration",
            ClientId = "step-up-client"
        };

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("CallbackRedirectUri", string.Join(' ', result.Failures ?? []), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenCompleteUsesCallbackRedirectFallback_Succeeds()
    {
        var validator = CreateValidator(
            ("Oidc:CallbackRedirectUri", "http://localhost:3000/callback"));
        var settings = new OidcStepUpSettings
        {
            DiscoveryEndpoint = "https://step-up.example/.well-known/openid-configuration",
            ClientId = "step-up-client"
        };

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenCompleteWithStepUpRedirect_Succeeds()
    {
        var validator = CreateValidator();
        var settings = new OidcStepUpSettings
        {
            DiscoveryEndpoint = "https://step-up.example/.well-known/openid-configuration",
            ClientId = "step-up-client",
            RedirectUri = "http://localhost:3000/callback"
        };

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOnlyStepUpRedirectUriSet_FailsForDiscoveryAndClient()
    {
        var validator = CreateValidator();
        var settings = new OidcStepUpSettings { RedirectUri = "http://localhost:3000/callback" };

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        var message = string.Join(' ', result.Failures ?? []);
        Assert.Contains("Oidc:StepUp:DiscoveryEndpoint", message, StringComparison.Ordinal);
        Assert.Contains("Oidc:StepUp:ClientId", message, StringComparison.Ordinal);
    }

}
