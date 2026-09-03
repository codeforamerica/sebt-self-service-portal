using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves the app fails to start when JwtSettings.SecretKey is missing or too short.
/// DC-313: Without ValidateDataAnnotations() on the JwtSettings registration,
/// the [Required] and [MinLength(32)] attributes are not enforced and the app
/// happily starts with an empty signing key — a critical security vulnerability.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class JwtSettingsStartupValidationTests : StartupValidationTestBase
{
    [Fact]
    public void Startup_WithEmptyJwtSecretKey_ThrowsOptionsValidationException()
    {
        // appsettings.json has SecretKey: "" and we deliberately don't override it
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", "");
        using var factory = CreateFactory();

        // ValidateOnStart triggers during host startup — CreateClient() surfaces the failure
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("SecretKey", ex.Message);
    }

    [Fact]
    public void Startup_WithTooShortJwtSecretKey_ThrowsOptionsValidationException()
    {
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", "too-short");
        using var factory = CreateFactory();

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("SecretKey", ex.Message);
    }
}
