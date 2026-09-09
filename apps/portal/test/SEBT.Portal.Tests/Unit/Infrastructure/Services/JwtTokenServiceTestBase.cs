using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Shared setup for JwtTokenService tests. Provides a fully configured
/// <see cref="JwtTokenService"/> instance with real <see cref="OidcVerificationClaimTranslator"/>
/// (not mocked) so tests exercise the full claim-resolution pipeline.
/// </summary>
public abstract class JwtTokenServiceTestBase
{
    /// <summary>
    /// ID proofing validity period used in all token service tests (≈5 years).
    /// Matches the default <see cref="IdProofingValiditySettings.ValidityDays"/> in test configuration.
    /// </summary>
    protected const int TestValidityDays = 1826;

    protected JwtTokenService Service { get; }

    protected JwtTokenServiceTestBase()
    {
        var jwtOptions = Substitute.For<IOptions<JwtSettings>>();
        jwtOptions.Value.Returns(new JwtSettings
        {
            SecretKey = "TestSecretKeyMustBeAtLeast32CharactersLongForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        });

        var validityOptions = Substitute.For<IOptions<IdProofingValiditySettings>>();
        validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = TestValidityDays });

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings { ValidityDays = TestValidityDays },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        Service = new JwtTokenService(jwtOptions, validityOptions, translator,
            NullLogger<JwtTokenService>.Instance);
    }

    protected static ClaimsPrincipal MakePrincipal(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)), "test");
        return new ClaimsPrincipal(identity);
    }

    protected static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);
}
