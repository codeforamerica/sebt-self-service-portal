using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class OidcTokenServiceTests : JwtTokenServiceTestBase
{
    // --- Normal login (non-step-up) ---

    [Fact]
    public void NormalLogin_WithVerificationClaims_SetsIal1Plus()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.Completed).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void NormalLogin_WithVerificationClaims_SetsCompletedAtAndExpiresAt()
    {
        var verifiedAt = DateTime.UtcNow.AddDays(-5);
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", verifiedAt.ToString("o")));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);

        var completedAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt);
        Assert.True(long.TryParse(completedAtClaim.Value, out var completedAtUnix));
        var completedAt = DateTimeOffset.FromUnixTimeSeconds(completedAtUnix).UtcDateTime;
        Assert.True(Math.Abs((completedAt - verifiedAt).TotalSeconds) < 1);

        var expiresAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var expiresAtUnix));
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime;
        var expectedExpiresAt = verifiedAt.AddDays(TestValidityDays);
        Assert.True(Math.Abs((expiresAt - expectedExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void NormalLogin_WithoutVerificationClaims_SetsIal1()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.NotStarted).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void NormalLogin_WithoutVerificationClaims_HasNoCompletedAtOrExpiresAt()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingCompletedAt));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingExpiresAt));
    }

    [Fact]
    public void NormalLogin_WithExpiredVerification_SetsIal1()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", DateTime.UtcNow.AddDays(-2000).ToString("o")));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.Expired).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void NormalLogin_WithIal1Verification_SetsIal1()
    {
        // Verification level 1.0 is "authenticated" — valid but doesn't elevate IAL
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.NotStarted).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    // --- Step-up ---

    [Fact]
    public void StepUp_WithVerificationClaims_ReturnsSuccessWithIal1Plus()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: true);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void StepUp_WithIal2VerificationClaims_SetsIal2()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "2"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: true);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("2", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void StepUp_WithoutVerificationClaims_ReturnsDependencyFailed()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: true);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<string>>(result);
    }

    // --- Claim handling ---

    [Fact]
    public void SubClaimIsAlwaysUserId_NotIdpSub()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var principal = MakePrincipal(
            ("sub", "idp-sub-999"),
            ("email", "user@example.com"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        var subClaim = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Equal(userId.ToString(), subClaim.Value);
    }

    [Fact]
    public void EmailComesFromIdpClaims()
    {
        var user = new User { Email = "old@example.com" };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "oidc@example.com"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        var emailClaim = jwt.Claims.First(c => c.Type == ClaimTypes.Email);
        Assert.Equal("oidc@example.com", emailClaim.Value);
    }

    [Fact]
    public void EmailFallsBackToUserEmail_WhenIdpHasNone()
    {
        var user = new User { Email = "fallback@example.com" };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        var emailClaim = jwt.Claims.First(c => c.Type == ClaimTypes.Email);
        Assert.Equal("fallback@example.com", emailClaim.Value);
    }

    [Fact]
    public void EmailFallsBackToEmpty_WhenNoEmailAnywhere()
    {
        var user = new User { Email = null };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        var emailClaim = jwt.Claims.First(c => c.Type == ClaimTypes.Email);
        Assert.Equal("", emailClaim.Value);
    }

    [Fact]
    public void ApplicationClaimsPassThrough()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("phone", "+13035551234"),
            ("givenName", "Jane"),
            ("familyName", "Doe"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        Assert.Equal("+13035551234", jwt.Claims.First(c => c.Type == "phone").Value);
        Assert.Equal("Jane", jwt.Claims.First(c => c.Type == "givenName").Value);
        Assert.Equal("Doe", jwt.Claims.First(c => c.Type == "familyName").Value);
    }

    [Fact]
    public void IsCoLoadedClaim_ComesFromUserEntity_NotIdp()
    {
        // The IdP has no concept of co-loaded — it comes from the DB user record.
        var user = new User { IsCoLoaded = true };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("true", jwt.Claims.First(c => c.Type == JwtClaimTypes.IsCoLoaded).Value);
    }

    [Fact]
    public void InfrastructureClaimsAreFilteredOut()
    {
        var user = new User();
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("iss", "https://idp.example.com"),
            ("aud", "client-id"),
            ("iat", "1700000000"),
            ("exp", "1700003600"),
            ("nonce", "abc123"),
            ("at_hash", "xyz789"));

        var result = Service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        // iss and aud should be the portal's values, not the IdP's
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
        // nonce and at_hash should not appear in the portal JWT
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "nonce"));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "at_hash"));
    }
}
