using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class SessionRefreshTokenServiceTests : JwtTokenServiceTestBase
{
    [Fact]
    public void CopiesIalFromExistingJwt()
    {
        var user = new User { IalLevel = UserIalLevel.None };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.Completed).ToString()),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void CopiesIdProofingTimestamps()
    {
        var user = new User();
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, "2"),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1700000000", jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt).Value);
        Assert.Equal("1857676800", jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt).Value);
    }

    [Fact]
    public void CopiesApplicationClaims()
    {
        var user = new User();
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "user@example.com"),
            ("phone", "+13035551234"),
            ("givenName", "Jane"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("+13035551234", jwt.Claims.First(c => c.Type == "phone").Value);
        Assert.Equal("Jane", jwt.Claims.First(c => c.Type == "givenName").Value);
    }

    [Fact]
    public void SubIsAlwaysUserId_NotFromExistingJwt()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var principal = MakePrincipal(
            (JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        var subClaims = jwt.Claims.Where(c => c.Type == JwtRegisteredClaimNames.Sub).ToList();
        Assert.Single(subClaims);
        Assert.Equal(userId.ToString(), subClaims[0].Value);
    }

    // --- Fallback to user entity ---

    [Fact]
    public void FallsBackToUserIal_WhenPrincipalLacksIal_Ial1Plus()
    {
        var user = new User { IalLevel = UserIalLevel.IAL1plus, Email = "user@example.com" };
        var principal = MakePrincipal(("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void FallsBackToUserIal_WhenPrincipalLacksIal_Ial2()
    {
        var user = new User { IalLevel = UserIalLevel.IAL2, Email = "user@example.com" };
        var principal = MakePrincipal(("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("2", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void FallsBackToUserIal_WhenPrincipalLacksIal_DefaultsTo1()
    {
        var user = new User { IalLevel = UserIalLevel.None, Email = "user@example.com" };
        var principal = MakePrincipal(("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void FallsBackToUserIdProofingStatus_WhenPrincipalLacksIt()
    {
        var user = new User { IdProofingStatus = IdProofingStatus.InProgress, Email = "user@example.com" };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal(
            ((int)IdProofingStatus.InProgress).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    // --- Email fallback chain ---

    [Fact]
    public void EmailFromPrincipalEmailClaim()
    {
        var user = new User { Email = "fallback@example.com" };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "principal@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("principal@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
    }

    [Fact]
    public void EmailFallsBackToEmpty_WhenNoEmailClaim()
    {
        var user = new User { Email = null };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
    }
}
