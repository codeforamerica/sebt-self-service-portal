using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var emailClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);

        Assert.NotNull(emailClaim);
        Assert.Equal(user.Email, emailClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldContainSubjectClaim()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var subClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);

        Assert.NotNull(subClaim);
        Assert.Equal(user.Email, subClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldContainJtiClaim()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jsonToken.Issuer);
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectAudience()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.Contains("TestAudience", jsonToken.Audiences);
    }

    [Fact]
    public void GenerateToken_ShouldHaveExpirationTime()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.True(jsonToken.ValidTo > DateTime.UtcNow);
        Assert.True(jsonToken.ValidTo <= DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void GenerateToken_ShouldGenerateDifferentTokensForSameUser()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token1 = _jwtTokenService.GenerateToken(user);
        var token2 = _jwtTokenService.GenerateToken(user);

        // Assert
        // Tokens should be different due to unique JTI
        Assert.NotEqual(token1, token2);

        // But should have same email claim
        var handler = new JwtSecurityTokenHandler();
        var token1Claims = handler.ReadJwtToken(token1);
        var token2Claims = handler.ReadJwtToken(token2);

        var email1 = token1Claims.Claims.First(c => c.Type == ClaimTypes.Email).Value;
        var email2 = token2Claims.Claims.First(c => c.Type == ClaimTypes.Email).Value;

        Assert.Equal(user.Email, email1);
        Assert.Equal(user.Email, email2);
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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = service.GenerateToken(user);

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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };
        var beforeGeneration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var token = _jwtTokenService.GenerateToken(user);

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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };
        var beforeGeneration = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var token = _jwtTokenService.GenerateToken(user);

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
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

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

    [Fact]
    public void GenerateToken_ShouldContainBothIdProofingStatusAndIalClaims()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IdProofingStatus = IdProofingStatus.Completed,
            IalLevel = UserIalLevel.IAL1plus
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var statusClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingStatus);
        var ialClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Ial);

        Assert.NotNull(statusClaim);
        Assert.Equal("2", statusClaim.Value); // Completed
        Assert.NotNull(ialClaim);
        Assert.Equal("1plus", ialClaim.Value); // IAL1plus
    }

    [Fact]
    public void GenerateToken_ShouldContainIdProofingSessionId_WhenSessionIdIsProvided()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1,
            IdProofingSessionId = "session-123-abc"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var sessionIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_session_id");

        Assert.NotNull(sessionIdClaim);
        Assert.Equal("session-123-abc", sessionIdClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldNotContainIdProofingSessionId_WhenSessionIdIsNull()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None,
            IdProofingSessionId = null
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var sessionIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_session_id");

        Assert.Null(sessionIdClaim);
    }

    [Fact]
    public void GenerateToken_ShouldContainIdProofingCompletedAt_WhenCompletedAtIsProvided()
    {
        // Arrange
        var completedAt = DateTime.UtcNow.AddDays(-5);
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingCompletedAt = completedAt
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var completedAtClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_completed_at");

        Assert.NotNull(completedAtClaim);
        Assert.True(long.TryParse(completedAtClaim.Value, out var unixTimestamp));
        var claimDateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
        // Allow 1 second tolerance for conversion
        Assert.True(Math.Abs((claimDateTime - completedAt).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateToken_ShouldContainIdProofingExpiresAt_WhenExpiresAtIsProvided()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingExpiresAt = expiresAt
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var expiresAtClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_expires_at");

        Assert.NotNull(expiresAtClaim);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var unixTimestamp));
        var claimDateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
        // Allow 1 second tolerance for conversion
        Assert.True(Math.Abs((claimDateTime - expiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateToken_ShouldIncludeAllIdProofingClaims_WhenUserHasCompleteData()
    {
        // Arrange
        var completedAt = DateTime.UtcNow.AddDays(-10);
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingSessionId = "session-xyz-789",
            IdProofingCompletedAt = completedAt,
            IdProofingExpiresAt = expiresAt
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        Assert.NotNull(jsonToken.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingStatus));
        Assert.NotNull(jsonToken.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Ial));
        Assert.NotNull(jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_session_id"));
        Assert.NotNull(jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_completed_at"));
        Assert.NotNull(jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_expires_at"));
    }

    [Fact]
    public void GenerateToken_WithAdditionalClaims_AddsIdpClaimsToToken()
    {
        // Arrange: OIDC complete-login passes IdP claims (phone, givenName, familyName) as additionalClaims
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };
        var additionalClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["phone"] = "+13035551234",
            ["givenName"] = "Jane",
            ["familyName"] = "Doe"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, additionalClaims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        Assert.Equal("+13035551234", jsonToken.Claims.FirstOrDefault(c => c.Type == "phone")?.Value);
        Assert.Equal("Jane", jsonToken.Claims.FirstOrDefault(c => c.Type == "givenName")?.Value);
        Assert.Equal("Doe", jsonToken.Claims.FirstOrDefault(c => c.Type == "familyName")?.Value);
    }

    [Fact]
    public void GenerateToken_WithAdditionalClaims_DoesNotDuplicateReservedClaims()
    {
        // Arrange: Callback token may include "sub" and "email"; we must not add them again or JWT payload
        // would have "sub": [a, b] which .NET's reader rejects
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.None
        };
        var additionalClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = "idp-sub-123",
            ["email"] = "other@example.com",
            ["phone"] = "+13035551234"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, additionalClaims);

        // Assert: sub and email remain the portal values (from user); phone is added
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var subClaims = jsonToken.Claims.Where(c => c.Type == JwtRegisteredClaimNames.Sub).ToList();
        var emailClaims = jsonToken.Claims.Where(c => c.Type == ClaimTypes.Email || c.Type == "email").ToList();
        Assert.Single(subClaims);
        Assert.Equal(user.Email, subClaims[0].Value);
        Assert.Single(emailClaims);
        Assert.Equal(user.Email, emailClaims[0].Value);
        Assert.Equal("+13035551234", jsonToken.Claims.FirstOrDefault(c => c.Type == "phone")?.Value);
    }

    [Fact]
    public void GenerateToken_WithNullAdditionalClaims_BehavesAsSingleArgumentOverload()
    {
        var user = new User { Email = "user@example.com", IalLevel = UserIalLevel.None };

        var token = _jwtTokenService.GenerateToken(user, null);

        Assert.NotNull(token);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        Assert.Equal(user.Email, jsonToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }
}

