using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class JwtTokenServiceTests
{
    private readonly IOptions<JwtSettings> _options = Substitute.For<IOptions<JwtSettings>>();
    private readonly JwtTokenService _jwtTokenService;

    public JwtTokenServiceTests()
    {
        var settings = new JwtSettings
        {
            SecretKey = "TestSecretKeyMustBeAtLeast32CharactersLongForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        };
        _options.Value.Returns(settings);
        _jwtTokenService = new JwtTokenService(_options);
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidJwtToken()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Verify it's a valid JWT format
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void GenerateToken_ShouldContainEmailClaim()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var emailClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);

        Assert.NotNull(emailClaim);
        Assert.Equal(email, emailClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldContainSubjectClaim()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var subClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);

        Assert.NotNull(subClaim);
        Assert.Equal(email, subClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldContainJtiClaim()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var jtiClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

        Assert.NotNull(jtiClaim);
        Assert.NotEmpty(jtiClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectIssuer()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jsonToken.Issuer);
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectAudience()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.Contains("TestAudience", jsonToken.Audiences);
    }

    [Fact]
    public void GenerateToken_ShouldHaveExpirationTime()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.True(jsonToken.ValidTo > DateTime.UtcNow);
        Assert.True(jsonToken.ValidTo <= DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void GenerateToken_ShouldGenerateDifferentTokensForSameEmail()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token1 = _jwtTokenService.GenerateToken(email);
        var token2 = _jwtTokenService.GenerateToken(email);

        // Assert
        // Tokens should be different due to unique JTI
        Assert.NotEqual(token1, token2);

        // But should have same email claim
        var handler = new JwtSecurityTokenHandler();
        var token1Claims = handler.ReadJwtToken(token1);
        var token2Claims = handler.ReadJwtToken(token2);

        var email1 = token1Claims.Claims.First(c => c.Type == ClaimTypes.Email).Value;
        var email2 = token2Claims.Claims.First(c => c.Type == ClaimTypes.Email).Value;

        Assert.Equal(email, email1);
        Assert.Equal(email, email2);
    }

    [Fact]
    public void GenerateToken_ShouldRespectExpirationMinutes()
    {
        // Arrange
        var settings = new JwtSettings
        {
            SecretKey = "TestSecretKeyMustBeAtLeast32CharactersLongForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 30
        };
        var options = Substitute.For<IOptions<JwtSettings>>();
        options.Value.Returns(settings);
        var service = new JwtTokenService(options);
        var email = "user@example.com";

        // Act
        var token = service.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var expectedExpiration = DateTime.UtcNow.AddMinutes(30);

        // Allow 1 minute tolerance for test execution time
        Assert.True(jsonToken.ValidTo >= expectedExpiration.AddMinutes(-1));
        Assert.True(jsonToken.ValidTo <= expectedExpiration.AddMinutes(1));
    }

    [Fact]
    public void GenerateToken_ShouldContainIatClaim()
    {
        // Arrange
        var email = "user@example.com";
        var beforeGeneration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var iatClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);

        Assert.NotNull(iatClaim);
        Assert.True(long.TryParse(iatClaim.Value, out var iatValue));

        var afterGeneration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // IAT should be between before and after generation (allowing 1 second tolerance)
        Assert.True(iatValue >= beforeGeneration - 1);
        Assert.True(iatValue <= afterGeneration + 1);
    }

    [Fact]
    public void GenerateToken_ShouldContainNbfClaim()
    {
        // Arrange
        var email = "user@example.com";
        var beforeGeneration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var nbfClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Nbf);

        Assert.NotNull(nbfClaim);
        Assert.True(long.TryParse(nbfClaim.Value, out var nbfValue));

        var afterGeneration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // NBF should be between before and after generation (allowing 1 second tolerance)
        Assert.True(nbfValue >= beforeGeneration - 1);
        Assert.True(nbfValue <= afterGeneration + 1);
    }

    [Fact]
    public void GenerateToken_ShouldHaveIatAndNbfEqual()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var token = _jwtTokenService.GenerateToken(email);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var iatClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);
        var nbfClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Nbf);

        Assert.NotNull(iatClaim);
        Assert.NotNull(nbfClaim);
        // IAT and NBF should be equal (token is valid immediately)
        Assert.Equal(iatClaim.Value, nbfClaim.Value);
    }
}

