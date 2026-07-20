using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Proves that JwtTokenService rejects empty or too-short SecretKey values.
/// DC-313: Without this guard, an empty key signs tokens with HMAC-SHA256 over
/// a zero-length secret — tokens are technically "signed" but trivially forgeable.
/// </summary>
public class JwtSecretKeyValidationTests
{
    private static JwtTokenService CreateService(string secretKey)
    {
        var jwtOptions = Substitute.For<IOptions<JwtSettings>>();
        jwtOptions.Value.Returns(new JwtSettings
        {
            SecretKey = secretKey,
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        });

        var validityOptions = Substitute.For<IOptions<IdProofingValiditySettings>>();
        validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = 1826 });

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings { ValidityDays = 1826 },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        return new JwtTokenService(jwtOptions, validityOptions, translator,
            NullLogger<JwtTokenService>.Instance);
    }

    [Fact]
    public void BuildAndSignToken_EmptySecretKey_Throws()
    {
        var service = CreateService("");
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Assert.ThrowsAny<Exception>(
            () => service.BuildAndSignToken(Guid.CreateVersion7(), "user@example.com", claims));
    }

    [Fact]
    public void BuildAndSignToken_ShortSecretKey_Throws()
    {
        var service = CreateService("too-short");
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Assert.ThrowsAny<Exception>(
            () => service.BuildAndSignToken(Guid.CreateVersion7(), "user@example.com", claims));
    }

    [Fact]
    public void BuildAndSignToken_ValidSecretKey_Succeeds()
    {
        var service = CreateService("TestSecretKeyMustBeAtLeast32CharactersLongForSecurity");
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var token = service.BuildAndSignToken(Guid.CreateVersion7(), "user@example.com", claims);

        Assert.False(string.IsNullOrEmpty(token));
    }
}
