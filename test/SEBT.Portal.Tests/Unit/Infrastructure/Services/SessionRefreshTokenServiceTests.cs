using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class SessionRefreshTokenServiceTests : JwtTokenServiceTestBase
{
    // Never downgrade: defense-in-depth. An admin-edited or stale DB row should not
    // supersede a higher IAL already granted by the current session claim.
    [Fact]
    public void DoesNotDowngradeIal_WhenDbIsLowerThanClaim_NoneVsIal1Plus()
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

    // --- DocV elevation flow ---

    // Critical scenario from the DocV webhook bug: DB has been updated to IAL2/Completed
    // after a successful DocV webhook, but the current session JWT still carries the pre-
    // DocV ial=1/NotStarted claims. Refresh must adopt the elevated DB state, not the stale
    // claims, so the user can access the IAL1plus-gated dashboard.
    [Fact]
    public void ElevatesFromIal1NotStartedToIal2Completed_WhenDbHasPostDocVState()
    {
        var completedAt = new DateTime(2026, 4, 24, 17, 2, 31, DateTimeKind.Utc);
        var user = new User
        {
            IalLevel = UserIalLevel.IAL2,
            IdProofingStatus = IdProofingStatus.Completed,
            IdProofingCompletedAt = completedAt,
            Email = "user@example.com"
        };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.NotStarted).ToString()),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("2", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.Completed).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);

        var expectedCompletedAtUnix =
            new DateTimeOffset(completedAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString();
        Assert.Equal(
            expectedCompletedAtUnix,
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt).Value);
    }

    [Fact]
    public void ElevatesIalFromClaim1ToDbIal1Plus()
    {
        var user = new User { IalLevel = UserIalLevel.IAL1plus, Email = "user@example.com" };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.NotStarted).ToString()),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    // Defense-in-depth: a stale or admin-edited DB IAL below the active claim
    // must not demote the session. IAL1plus in claim wins over IAL1plus vs
    // IAL2 in claim wins over IAL1plus in DB.
    [Fact]
    public void DoesNotDowngradeIal_WhenDbIal1PlusAndClaimIal2()
    {
        var user = new User
        {
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingStatus = IdProofingStatus.Completed,
            IdProofingCompletedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Email = "user@example.com"
        };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "2"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.Completed).ToString()),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("2", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void FallsBackToDbIal_WhenPrincipalLacksIalClaim_Ial2()
    {
        var user = new User { IalLevel = UserIalLevel.IAL2, Email = "user@example.com" };
        var principal = MakePrincipal(("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("2", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void ElevatesIdProofingStatusToCompleted_WithCompletedAtTimestamp()
    {
        var completedAt = new DateTime(2026, 4, 24, 10, 30, 0, DateTimeKind.Utc);
        var user = new User
        {
            IalLevel = UserIalLevel.IAL2,
            IdProofingStatus = IdProofingStatus.Completed,
            IdProofingCompletedAt = completedAt,
            Email = "user@example.com"
        };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.NotStarted).ToString()),
            ("email", "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal(
            ((int)IdProofingStatus.Completed).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);

        var expectedCompletedAtUnix =
            new DateTimeOffset(completedAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString();
        Assert.Equal(
            expectedCompletedAtUnix,
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt).Value);
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

    // BuildAndSignToken writes the email claim under ClaimTypes.Email and JWT bearer
    // runs with MapInboundClaims=false, so the refresh principal carries the long URI
    // form. GenerateForSessionRefresh must resolve the email from either form or the
    // refreshed JWT will silently blank the user's email.
    [Fact]
    public void PreservesEmail_WhenPrincipalEmailUsesLongFormClaimType()
    {
        var user = new User { IalLevel = UserIalLevel.IAL1plus, Email = "user@example.com" };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.Completed).ToString()),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            (ClaimTypes.Email, "user@example.com"));

        var token = Service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("user@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
    }
}
