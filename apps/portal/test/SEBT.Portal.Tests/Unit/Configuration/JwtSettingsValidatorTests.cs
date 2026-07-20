using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class JwtSettingsValidatorTests
{
    private static JwtSettings ValidSettings() => new()
    {
        SecretKey = new string('x', 32),
        Issuer = "test",
        Audience = "test",
        ExpirationMinutes = 15,
        AbsoluteExpirationMinutes = 60
    };

    [Fact]
    public void Validate_Succeeds_WhenAbsoluteIsGreaterThanIdle()
    {
        var settings = ValidSettings();
        var validator = new JwtSettingsValidator();

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Succeeds_WhenAbsoluteEqualsIdle()
    {
        var settings = ValidSettings();
        settings.ExpirationMinutes = 30;
        settings.AbsoluteExpirationMinutes = 30;
        var validator = new JwtSettingsValidator();

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenAbsoluteIsLessThanIdle()
    {
        var settings = ValidSettings();
        settings.ExpirationMinutes = 30;
        settings.AbsoluteExpirationMinutes = 15;
        var validator = new JwtSettingsValidator();

        var result = validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("AbsoluteExpirationMinutes", result.FailureMessage);
        Assert.Contains("ExpirationMinutes", result.FailureMessage);
    }

    [Fact]
    public void Validate_Fails_WhenSettingsAreNull()
    {
        var validator = new JwtSettingsValidator();

        var result = validator.Validate(null, null!);

        Assert.True(result.Failed);
    }

    [Fact]
    public void DefaultJwtSettings_HasIdleAt15AndAbsoluteAt60()
    {
        // Locks the documented defaults: idle 15 min, absolute 60 min.
        // Both are tighter than the NIST SP 800-63B IAL2 ceilings (30 min idle, 12 hr absolute).
        var settings = new JwtSettings();

        Assert.Equal(15, settings.ExpirationMinutes);
        Assert.Equal(60, settings.AbsoluteExpirationMinutes);
    }
}
