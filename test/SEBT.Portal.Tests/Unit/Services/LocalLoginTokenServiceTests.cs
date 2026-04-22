using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class LocalLoginTokenServiceTests
{
    private readonly ILocalLoginTokenService _service;

    public LocalLoginTokenServiceTests()
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

    private static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void SubClaimIsUserId()
    {
        var user = new User { Id = 42, Email = "user@example.com" };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("42", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public void EmailComesFromUserEntity()
    {
        var user = new User { Id = 1, Email = "otp-user@example.com" };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("otp-user@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
    }

    [Fact]
    public void IalComesFromUserIalLevel()
    {
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1plus };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void IdProofingStatusComesFromUser()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IdProofingStatus = IdProofingStatus.Completed,
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingCompletedAt = DateTime.UtcNow.AddDays(-5)
        };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal(
            ((int)IdProofingStatus.Completed).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void CompletedUser_HasCompletedAtAndExpiresAt()
    {
        var completedAt = DateTime.UtcNow.AddDays(-10);
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingStatus = IdProofingStatus.Completed,
            IdProofingCompletedAt = completedAt
        };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        var completedAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt);
        Assert.True(long.TryParse(completedAtClaim.Value, out _));

        var expiresAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var expiresAtUnix));
        var expectedExpiresAt = completedAt.AddDays(1826);
        var actualExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime;
        Assert.True(Math.Abs((actualExpiresAt - expectedExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void UserWithNoProofing_HasNoCompletedAtOrExpiresAt()
    {
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.None };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingCompletedAt));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingExpiresAt));
    }

    [Fact]
    public void IncludesIdProofingSessionId_WhenPresent()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IdProofingSessionId = "session-abc-123"
        };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("session-abc-123",
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingSessionId).Value);
    }

    [Fact]
    public void OmitsIdProofingSessionId_WhenNull()
    {
        var user = new User { Id = 1, Email = "user@example.com", IdProofingSessionId = null };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingSessionId));
    }

    [Fact]
    public void ProducesValidJwtWithStandardClaims()
    {
        var user = new User { Id = 1, Email = "user@example.com" };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti));
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat));
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }
}
