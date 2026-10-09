using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class OidcVerificationClaimSettingsValidatorTests
{
    private static OidcVerificationClaimSettingsValidator CreateValidator(bool oidcEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:DiscoveryEndpoint"] = oidcEnabled ? "https://auth.example.com/.well-known/openid-configuration" : null,
            })
            .Build();
        return new OidcVerificationClaimSettingsValidator(configuration);
    }

    [Fact]
    public void Validate_WhenOidcDisabled_SucceedsWithoutClaimNames()
    {
        var result = CreateValidator(oidcEnabled: false).Validate(null, new OidcVerificationClaimSettings());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOidcEnabledAndClaimNamesSetWithoutFallbacks_Succeeds()
    {
        var settings = new OidcVerificationClaimSettings { LevelClaimName = "idpLevel", DateClaimName = "idpDate" };

        var result = CreateValidator(oidcEnabled: true).Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenOidcEnabledAndLevelClaimNameMissing_Fails(string? levelClaimName)
    {
        var settings = new OidcVerificationClaimSettings { LevelClaimName = levelClaimName, DateClaimName = "idpDate" };

        var result = CreateValidator(oidcEnabled: true).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Oidc:VerificationClaims:LevelClaimName", result.FailureMessage);
        Assert.DoesNotContain("DateClaimName", result.FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenOidcEnabledAndDateClaimNameMissing_Fails(string? dateClaimName)
    {
        var settings = new OidcVerificationClaimSettings { LevelClaimName = "idpLevel", DateClaimName = dateClaimName };

        var result = CreateValidator(oidcEnabled: true).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Oidc:VerificationClaims:DateClaimName", result.FailureMessage);
        Assert.DoesNotContain("LevelClaimName", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenOidcEnabledAndBothClaimNamesMissing_ReportsBoth()
    {
        var result = CreateValidator(oidcEnabled: true).Validate(null, new OidcVerificationClaimSettings());

        Assert.True(result.Failed);
        Assert.Contains("Oidc:VerificationClaims:LevelClaimName", result.FailureMessage);
        Assert.Contains("Oidc:VerificationClaims:DateClaimName", result.FailureMessage);
    }
}
