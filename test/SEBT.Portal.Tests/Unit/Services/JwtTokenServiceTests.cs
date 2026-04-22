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
    private readonly IOptions<IdProofingValiditySettings> _validityOptions = Substitute.For<IOptions<IdProofingValiditySettings>>();
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
        _validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = 1826 });
        _jwtTokenService = new JwtTokenService(_options, _validityOptions);
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
        // Arrange — sub is always the internal user ID, not the email
        var user = new User
        {
            Id = 1,
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
        Assert.Equal(user.Id.ToString(), subClaim.Value);
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
        var validityOpts = Substitute.For<IOptions<IdProofingValiditySettings>>();
        validityOpts.Value.Returns(new IdProofingValiditySettings());
        var service = new JwtTokenService(options, validityOpts);
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
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingCompletedAt = DateTime.UtcNow.AddDays(-5)
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
    public void GenerateToken_ShouldComputeIdProofingExpiresAt_FromCompletedAtPlusValidityDuration()
    {
        // Arrange — expiration is now computed from IdProofingCompletedAt + validity duration,
        // not read from the (obsolete) IdProofingExpiresAt field.
        var completedAt = DateTime.UtcNow.AddDays(-10);
        var expectedExpiresAt = completedAt.AddDays(1826); // ValidityDays = 1826
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
        var expiresAtClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "id_proofing_expires_at");

        Assert.NotNull(expiresAtClaim);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var unixTimestamp));
        var claimDateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
        Assert.True(Math.Abs((claimDateTime - expectedExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateToken_ShouldIncludeAllIdProofingClaims_WhenUserHasCompleteData()
    {
        // Arrange — id_proofing_expires_at is now computed from IdProofingCompletedAt
        var completedAt = DateTime.UtcNow.AddDays(-10);
        var user = new User
        {
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingSessionId = "session-xyz-789",
            IdProofingCompletedAt = completedAt
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
        // Arrange: additionalClaims may include "sub" and "email"; sub is always user.Id.ToString()
        // (not overridable), email is overridable via additionalClaims, and each claim type appears
        // exactly once (no duplicates).
        var user = new User
        {
            Id = 1,
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

        // Assert: sub is always user.Id, not additionalClaims sub; email comes from additionalClaims;
        // each claim type appears exactly once; phone is added
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        var subClaims = jsonToken.Claims.Where(c => c.Type == JwtRegisteredClaimNames.Sub).ToList();
        var emailClaims = jsonToken.Claims.Where(c => c.Type == ClaimTypes.Email || c.Type == "email").ToList();
        Assert.Single(subClaims);
        Assert.Equal(user.Id.ToString(), subClaims[0].Value);
        Assert.Single(emailClaims);
        Assert.Equal("other@example.com", emailClaims[0].Value);
        Assert.Equal("+13035551234", jsonToken.Claims.FirstOrDefault(c => c.Type == "phone")?.Value);
    }

    [Fact]
    public void GenerateToken_WithNullAdditionalClaims_BehavesAsSingleArgumentOverload()
    {
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.None };

        var token = _jwtTokenService.GenerateToken(user, null);

        Assert.NotNull(token);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jsonToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsContainEmail_UsesClaimsEmail()
    {
        // Arrange: OIDC user has no stored email; email comes from IdP claims
        var user = new User { Id = 1, Email = null };
        var claims = new Dictionary<string, string>
        {
            ["email"] = "oidc-user@example.com",
            ["sub"] = "pingone-sub-123"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var emailClaim = jwt.Claims.First(c => c.Type == ClaimTypes.Email);
        Assert.Equal("oidc-user@example.com", emailClaim.Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsContainIal_UsesClaimsIal()
    {
        // Arrange: OIDC user has stale IAL in DB; fresh IAL comes from IdP claims
        var user = new User { Id = 1, IalLevel = UserIalLevel.None };
        var claims = new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            [JwtClaimTypes.Ial] = "1plus"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var ialClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial);
        Assert.Equal("1plus", ialClaim.Value);
    }

    [Fact]
    public void GenerateToken_WhenNoAdditionalClaims_UsesUserProperties()
    {
        // Arrange: OTP user — email and IAL come from the user object (DB values)
        var user = new User
        {
            Id = 1,
            Email = "otp-user@example.com",
            IalLevel = UserIalLevel.IAL1plus
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal("otp-user@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsContainSub_SubIsAlwaysUserId()
    {
        // Arrange: OIDC user — sub in the portal JWT is always user.Id.ToString(),
        // even when additionalClaims carries the IdP's sub. The IdP sub is stored as
        // ExternalProviderId in the DB and is not propagated into the portal JWT.
        var user = new User { Id = 1, Email = null };
        var claims = new Dictionary<string, string>
        {
            ["email"] = "oidc-user@example.com",
            ["sub"] = "pingone-sub-456"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var subClaim = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Equal(user.Id.ToString(), subClaim.Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsContainIdProofingStatus_UsesClaimsValue()
    {
        // Arrange: OIDC user — ID proofing status comes from claims
        var user = new User
        {
            Id = 1,
            Email = null,
            IdProofingStatus = IdProofingStatus.NotStarted
        };
        var claims = new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            [JwtClaimTypes.IdProofingStatus] = "2", // Completed
            [JwtClaimTypes.Ial] = "1plus",
            [JwtClaimTypes.IdProofingCompletedAt] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var statusClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus);
        Assert.Equal("2", statusClaim.Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsContainIdProofingSessionId_UsesClaimsValue()
    {
        // Arrange: OIDC user — session ID comes from claims, not user object
        var user = new User
        {
            Id = 1,
            Email = null,
            IdProofingSessionId = null
        };
        var claims = new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            [JwtClaimTypes.IdProofingSessionId] = "oidc-session-xyz"
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sessionIdClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingSessionId);
        Assert.Equal("oidc-session-xyz", sessionIdClaim.Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsContainIdProofingTimestamps_UsesClaimsValues()
    {
        // Arrange: OIDC user — completed_at and expires_at come from claims
        var user = new User
        {
            Id = 1,
            Email = null,
            IdProofingCompletedAt = null
        };
        var completedAtUnix = "1700000000";
        var expiresAtUnix = "1857676800";
        var claims = new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            [JwtClaimTypes.IdProofingCompletedAt] = completedAtUnix,
            [JwtClaimTypes.IdProofingExpiresAt] = expiresAtUnix
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal(completedAtUnix, jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt).Value);
        Assert.Equal(expiresAtUnix, jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt).Value);
    }

    [Fact]
    public void GenerateToken_WhenAdditionalClaimsHaveCompletedAtButNoExpiresAt_ComputesExpiresAt()
    {
        // Arrange: OIDC step-up path — ApplyVerificationToClaims sets CompletedAt but
        // not ExpiresAt, and the user entity has no IdProofingCompletedAt (OIDC users
        // don't persist IAL to DB). Without the fix, neither fallback branch fires and
        // IdProofingExpiresAt is missing from the JWT, causing the frontend IalGuard
        // to loop the user back into step-up indefinitely.
        var completedAt = DateTimeOffset.UtcNow.AddDays(-5);
        var completedAtUnix = completedAt.ToUnixTimeSeconds().ToString();
        var expectedExpiresAt = completedAt.UtcDateTime.AddDays(1826); // ValidityDays = 1826

        var user = new User
        {
            Id = 1,
            Email = null,
            IdProofingCompletedAt = null // OIDC users don't persist to DB
        };
        var claims = new Dictionary<string, string>
        {
            ["email"] = "user@example.com",
            [JwtClaimTypes.Ial] = "1plus",
            [JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString(),
            [JwtClaimTypes.IdProofingCompletedAt] = completedAtUnix
            // Note: no IdProofingExpiresAt — this is the bug scenario
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, claims);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var expiresAtClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingExpiresAt);

        Assert.NotNull(expiresAtClaim);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var expiresAtUnix));
        var claimDateTime = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime;
        Assert.True(Math.Abs((claimDateTime - expectedExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateToken_WhenIdProofingCompletedButIalIsOne_Throws()
    {
        // Arrange: IdProofingStatus=Completed with IAL=1 is an invariant violation.
        // This combination means "we completed identity proofing but the user is still
        // at the lowest assurance level" — which should never happen. Minting a JWT
        // with these contradictory claims would confuse downstream guards.
        var user = new User { Id = 1, Email = "user@example.com" };
        var claims = new Dictionary<string, string>
        {
            [JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString(),
            [JwtClaimTypes.Ial] = "1",
            [JwtClaimTypes.IdProofingCompletedAt] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _jwtTokenService.GenerateToken(user, claims));
    }

    [Fact]
    public void GenerateToken_WhenIdProofingCompletedButNoCompletedAt_Throws()
    {
        // Arrange: IdProofingStatus=Completed without a CompletedAt timestamp is an
        // invariant violation. Without the timestamp we can't compute expiration, and
        // the frontend IalGuard will reject the session (missing idProofingExpiresAt).
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IdProofingCompletedAt = null // no DB timestamp either
        };
        var claims = new Dictionary<string, string>
        {
            [JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString(),
            [JwtClaimTypes.Ial] = "1plus"
            // Note: no IdProofingCompletedAt in claims
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _jwtTokenService.GenerateToken(user, claims));
    }
}

