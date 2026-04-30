using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class OidcVerificationClaimTranslatorTests
{
    private readonly OidcVerificationClaimSettings _claimSettings = new();
    private readonly IdProofingValiditySettings _validitySettings = new() { ValidityDays = 1826 };

    private OidcVerificationClaimTranslator CreateTranslator(
        OidcVerificationClaimSettings? claimSettings = null,
        IdProofingValiditySettings? validitySettings = null)
    {
        return new OidcVerificationClaimTranslator(
            claimSettings ?? _claimSettings,
            validitySettings ?? _validitySettings,
            NullLogger<OidcVerificationClaimTranslator>.Instance);
    }

    [Fact]
    public void Translate_returns_null_when_level_claim_missing()
    {
        var claims = new Dictionary<string, string> { ["otherClaim"] = "value" };
        var result = CreateTranslator().Translate(claims);
        Assert.Null(result);
    }

    [Fact]
    public void Translate_returns_null_when_level_claim_empty()
    {
        var claims = new Dictionary<string, string> { ["socureIdVerificationLevel"] = "" };
        var result = CreateTranslator().Translate(claims);
        Assert.Null(result);
    }

    [Fact]
    public void Translate_returns_null_when_level_is_unrecognized()
    {
        var claims = new Dictionary<string, string> { ["socureIdVerificationLevel"] = "3.0" };
        var result = CreateTranslator().Translate(claims);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("1", UserIalLevel.IAL1)]
    [InlineData("1.0", UserIalLevel.IAL1)]
    [InlineData("1.5", UserIalLevel.IAL1plus)]
    [InlineData("1.50", UserIalLevel.IAL1plus)]
    [InlineData("2", UserIalLevel.IAL2)]
    [InlineData("2.0", UserIalLevel.IAL2)]
    public void Translate_maps_level_values_to_IAL(string levelValue, UserIalLevel expectedIal)
    {
        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = levelValue,
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(expectedIal, result.IalLevel);
    }

    [Fact]
    public void Translate_valid_level_with_fresh_date_is_not_expired()
    {
        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.False(result.IsExpired);
    }

    [Fact]
    public void Translate_valid_level_with_expired_date_is_expired()
    {
        var verificationDate = DateTime.UtcNow.AddYears(-6).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.True(result.IsExpired);
    }

    [Fact]
    public void Translate_valid_level_without_date_claim_uses_current_time()
    {
        // When the OIDC provider doesn't include a date, VerifiedAt falls back to
        // DateTime.UtcNow so the expiration clock starts from "now".
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5"
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.False(result.IsExpired);
        // VerifiedAt should be approximately now (within a few seconds)
        Assert.InRange(result.VerifiedAt, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Translate_valid_level_with_unparseable_date_uses_current_time()
    {
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = "not-a-date"
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.False(result.IsExpired);
    }

    [Fact]
    public void Translate_parses_verification_date()
    {
        var expected = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = expected.ToString("o")
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(expected, result.VerifiedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Translate_uses_custom_claim_names()
    {
        var customSettings = new OidcVerificationClaimSettings
        {
            LevelClaimName = "myIdpLevel",
            DateClaimName = "myIdpDate"
        };
        var claims = new Dictionary<string, string>
        {
            ["myIdpLevel"] = "1.5",
            ["myIdpDate"] = DateTime.UtcNow.AddDays(-1).ToString("o")
        };

        var result = CreateTranslator(claimSettings: customSettings).Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
    }

    [Fact]
    public void Translate_uses_myCoIdVerificationLevel_when_primary_claim_missing()
    {
        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["myCoIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
        Assert.False(result.IsExpired);
    }

    [Fact]
    public void Translate_uses_myCoIdVerificationLevel_when_primary_level_unrecognized()
    {
        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "3.0",
            ["myCoIdVerificationLevel"] = "2",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(UserIalLevel.IAL2, result.IalLevel);
    }

    [Fact]
    public void Translate_prefers_configured_level_claim_when_myCo_id_verification_level_also_present()
    {
        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["myCoIdVerificationLevel"] = "2",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
    }

    [Fact]
    public void Translate_uses_myCoIdVerificationDate_when_primary_date_missing()
    {
        var expected = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["myCoIdVerificationDate"] = expected.ToString("o")
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(expected, result.VerifiedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Translate_prefers_configured_date_claim_when_myCo_id_verification_date_also_present()
    {
        var primaryDate = new DateTime(2025, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var fallbackDate = new DateTime(2025, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = primaryDate.ToString("o"),
            ["myCoIdVerificationDate"] = fallbackDate.ToString("o")
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(primaryDate, result.VerifiedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Translate_uses_myCoIdVerificationDate_when_primary_date_unparseable()
    {
        var expected = new DateTime(2025, 6, 10, 15, 30, 0, DateTimeKind.Utc);
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = "not-a-date",
            ["myCoIdVerificationDate"] = expected.ToString("o")
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(expected, result.VerifiedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Translate_uses_fallback_claim_names_from_settings_when_primary_missing()
    {
        var settings = new OidcVerificationClaimSettings
        {
            FallbackLevelClaimName = "customLevelFallback",
            FallbackDateClaimName = "customDateFallback"
        };
        var expectedDate = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        var claims = new Dictionary<string, string>
        {
            ["customLevelFallback"] = "1.5",
            ["customDateFallback"] = expectedDate.ToString("o")
        };

        var result = CreateTranslator(claimSettings: settings).Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
        Assert.Equal(expectedDate, result.VerifiedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Translate_respects_custom_validity_duration()
    {
        var shortValidity = new IdProofingValiditySettings { ValidityDays = 365 };
        var verificationDate = DateTime.UtcNow.AddMonths(-18).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator(validitySettings: shortValidity).Translate(claims);

        Assert.NotNull(result);
        Assert.True(result.IsExpired);
    }

    [Fact]
    public void Translate_at_exact_boundary_is_expired()
    {
        var validity = new IdProofingValiditySettings { ValidityDays = 365 };
        // Set verification date to exactly 365 days ago (should be expired)
        var verificationDate = DateTime.UtcNow.AddDays(-365).AddSeconds(-1).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator(validitySettings: validity).Translate(claims);

        Assert.NotNull(result);
        Assert.True(result.IsExpired);
    }
}
