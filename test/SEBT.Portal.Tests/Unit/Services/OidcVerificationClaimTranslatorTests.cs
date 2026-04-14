using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class OidcVerificationClaimTranslatorTests
{
    private readonly OidcVerificationClaimSettings _claimSettings = new();
    private readonly IdProofingValiditySettings _validitySettings = new() { ValidityYears = 5 };

    private OidcVerificationClaimTranslator CreateTranslator(
        OidcVerificationClaimSettings? claimSettings = null,
        IdProofingValiditySettings? validitySettings = null)
    {
        return new OidcVerificationClaimTranslator(
            claimSettings ?? _claimSettings,
            validitySettings ?? _validitySettings);
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
    [InlineData("1.5")]
    [InlineData("1.50")]
    public void Translate_maps_1_5_to_IAL1plus(string levelValue)
    {
        var verificationDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = levelValue,
            ["socureIdVerificationDate"] = verificationDate
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
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
    public void Translate_valid_level_without_date_claim_is_not_expired()
    {
        // When the OIDC provider doesn't include a date, we trust the level claim.
        // VerifiedAt is null (we don't fabricate a date), but the verification is
        // treated as fresh since we can't prove it's stale.
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5"
        };

        var result = CreateTranslator().Translate(claims);

        Assert.NotNull(result);
        Assert.False(result.IsExpired);
        Assert.Null(result.VerifiedAt);
    }

    [Fact]
    public void Translate_valid_level_with_unparseable_date_is_not_expired()
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
        Assert.NotNull(result.VerifiedAt);
        Assert.Equal(expected, result.VerifiedAt.Value, TimeSpan.FromSeconds(1));
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
    public void Translate_respects_custom_validity_duration()
    {
        var shortValidity = new IdProofingValiditySettings { ValidityYears = 1 };
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
        var validity = new IdProofingValiditySettings { ValidityYears = 1 };
        // Set verification date to exactly 1 year ago (should be expired)
        var verificationDate = DateTime.UtcNow.AddYears(-1).AddSeconds(-1).ToString("o");
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
