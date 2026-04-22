using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class SessionRefreshTokenServiceTests
{
    private readonly ISessionRefreshTokenService _service;

    public SessionRefreshTokenServiceTests()
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
        validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = 1826 });

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings { ValidityDays = 1826 },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        _service = new JwtTokenService(jwtOptions, validityOptions, translator);
    }

    private static ClaimsPrincipal MakePrincipal(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)), "test");
        return new ClaimsPrincipal(identity);
    }

    private static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void CopiesIalFromExistingJwt()
    {
        var user = new User { Id = 1, IalLevel = UserIalLevel.None };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.Completed).ToString()),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void CopiesIdProofingTimestamps()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, "2"),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1700000000", jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt).Value);
        Assert.Equal("1857676800", jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt).Value);
    }

    [Fact]
    public void CopiesApplicationClaims()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "user@example.com"),
            ("phone", "+13035551234"),
            ("givenName", "Jane"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("+13035551234", jwt.Claims.First(c => c.Type == "phone").Value);
        Assert.Equal("Jane", jwt.Claims.First(c => c.Type == "givenName").Value);
    }

    [Fact]
    public void SubIsAlwaysUserId_NotFromExistingJwt()
    {
        var user = new User { Id = 42 };
        var principal = MakePrincipal(
            (JwtRegisteredClaimNames.Sub, "999"),
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        var subClaims = jwt.Claims.Where(c => c.Type == JwtRegisteredClaimNames.Sub).ToList();
        Assert.Single(subClaims);
        Assert.Equal("42", subClaims[0].Value);
    }

    [Fact]
    public void FallsBackToUserEntity_WhenPrincipalLacksIal()
    {
        var user = new User { Id = 1, IalLevel = UserIalLevel.IAL1plus, Email = "user@example.com" };
        var principal = MakePrincipal(("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }
}
