using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class OidcSettingsValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_PassesForNullOrEmptyKeys(string? value)
    {
        // Arrange
        var validator = new OidcSettingsValidator();
        var settings = CreateTestSettings(value);

        // Act
        var result = validator.Validate(name: null, options: settings);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Validate_FailsForTooShortSigningKey(int charCount)
    {
        // Arrange
        var validator = new OidcSettingsValidator();
        var settings = CreateTestSettings(new string('x', charCount));

        // Act
        var result = validator.Validate(name: null, options: settings);

        // Assert
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void Validate_PassesFor32CharOrGreaterKey(int charCount)
    {
        // Arrange
        var validator = new OidcSettingsValidator();
        var key = new string('x', charCount);
        var settings = CreateTestSettings(key);

        // Act
        var result = validator.Validate(name: null, options: settings);

        // Assert
        Assert.True(result.Succeeded);
    }

    private static OidcSettings CreateTestSettings(string? key)
    {
        return new OidcSettings
        {
            DiscoveryEndpoint = "https://example.com/.well-known/openid-configuration",
            ClientId = "TEST_CLIENT_ID",
            ClientSecret = "TEST_CLIENT_SECRET",
            CallbackRedirectUri = "https://example.com/oidc-callback",
            CompleteLoginSigningKey = key
        };
    }
}
