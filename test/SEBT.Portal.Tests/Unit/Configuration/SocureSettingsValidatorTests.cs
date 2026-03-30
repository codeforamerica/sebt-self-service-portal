using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class SocureSettingsValidatorTests
{
    private static SocureSettingsValidator CreateValidator(string environmentName = "Development")
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new SocureSettingsValidator(environment);
    }

    private static SocureSettings CreateValidStubSettings() => new()
    {
        Enabled = true,
        UseStub = true,
        ChallengeExpirationMinutes = 30
    };

    private static SocureSettings CreateValidRealSettings() => new()
    {
        Enabled = true,
        UseStub = false,
        ApiKey = "test-api-key",
        WebhookSecret = "test-webhook-secret",
        ChallengeExpirationMinutes = 30
    };

    // --- Enabled flag (short-circuit when Socure is disabled) ---

    [Fact]
    public void Validate_ShouldSucceed_WhenNotEnabled()
    {
        var validator = CreateValidator("Production");
        var settings = new SocureSettings { Enabled = false };

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenNotEnabled_EvenWithInvalidSettings()
    {
        var validator = CreateValidator("Production");
        var settings = new SocureSettings
        {
            Enabled = false,
            UseStub = true, // would normally fail in Production
            ChallengeExpirationMinutes = 0 // would normally fail range check
        };

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // --- UseStub environment guard ---

    [Fact]
    public void Validate_ShouldFail_WhenUseStubTrueOutsideDevelopment()
    {
        var validator = CreateValidator("Production");
        var settings = CreateValidStubSettings();

        var result = validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("UseStub", result.Failures!.Single());
    }

    [Fact]
    public void Validate_ShouldFail_WhenUseStubTrueInStaging()
    {
        var validator = CreateValidator("Staging");
        var settings = CreateValidStubSettings();

        var result = validator.Validate(null, settings);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUseStubTrueInDevelopment()
    {
        var validator = CreateValidator("Development");
        var settings = CreateValidStubSettings();

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUseStubFalseWithRequiredSettings()
    {
        var validator = CreateValidator("Production");
        var settings = CreateValidRealSettings();

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // --- Existing validation behavior (preserved) ---

    [Fact]
    public void Validate_ShouldFail_WhenOptionsNull()
    {
        var validator = CreateValidator();

        var result = validator.Validate(null, null!);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldFail_WhenChallengeExpirationOutOfRange()
    {
        var validator = CreateValidator();
        var settings = CreateValidStubSettings();
        settings.ChallengeExpirationMinutes = 0;

        var result = validator.Validate(null, settings);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldFail_WhenUseStubFalseAndApiKeyMissing()
    {
        var validator = CreateValidator("Production");
        var settings = new SocureSettings
        {
            Enabled = true,
            UseStub = false,
            WebhookSecret = "secret",
            ChallengeExpirationMinutes = 30
        };

        var result = validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("ApiKey", result.Failures!.Single());
    }

    [Fact]
    public void Validate_ShouldFail_WhenUseStubFalseAndWebhookSecretMissing()
    {
        var validator = CreateValidator("Production");
        var settings = new SocureSettings
        {
            Enabled = true,
            UseStub = false,
            ApiKey = "key",
            ChallengeExpirationMinutes = 30
        };

        var result = validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("WebhookSecret", result.Failures!.Single());
    }
}
