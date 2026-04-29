using System.IdentityModel.Tokens.Jwt;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Tests for the internal <see cref="JwtTokenService.BuildAndSignToken"/> method,
/// focusing on invariant enforcement. These guards are defense-in-depth — the public
/// methods resolve claims correctly so these paths are structurally unreachable in
/// normal operation. We test them directly to ensure they fire if future changes
/// introduce inconsistent claim state.
/// </summary>
public class BuildAndSignTokenTests : JwtTokenServiceTestBase
{
    [Fact]
    public void Completed_WithIal1_Throws()
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [JwtClaimTypes.Ial] = "1",
            [JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString(),
            [JwtClaimTypes.IdProofingCompletedAt] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => Service.BuildAndSignToken(Guid.NewGuid(), "user@example.com", claims));

        Assert.Contains("IAL=1", ex.Message);
    }

    [Fact]
    public void Completed_WithoutCompletedAt_Throws()
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [JwtClaimTypes.Ial] = "1plus",
            [JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString()
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => Service.BuildAndSignToken(Guid.NewGuid(), "user@example.com", claims));

        Assert.Contains("completion timestamp", ex.Message);
    }

    [Fact]
    public void Completed_WithValidState_ProducesJwt()
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [JwtClaimTypes.Ial] = "1plus",
            [JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString(),
            [JwtClaimTypes.IdProofingCompletedAt] = "1700000000",
            [JwtClaimTypes.IdProofingExpiresAt] = "1857676800"
        };

        var userId = Guid.NewGuid();
        var token = Service.BuildAndSignToken(userId, "user@example.com", claims);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public void FallsBackToDefaults_WhenIalAndStatusMissing()
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var token = Service.BuildAndSignToken(Guid.NewGuid(), "user@example.com", claims);

        var jwt = ReadJwt(token);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.NotStarted).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void PassthroughClaims_SkipsReservedNames()
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [JwtClaimTypes.Ial] = "1",
            [JwtClaimTypes.IdProofingStatus] = "0",
            ["phone"] = "+13035551234",
            ["sub"] = "should-be-ignored",
            ["email"] = "should-be-ignored"
        };

        var userId = Guid.NewGuid();
        var token = Service.BuildAndSignToken(userId, "actual@example.com", claims);

        var jwt = ReadJwt(token);
        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("+13035551234", jwt.Claims.First(c => c.Type == "phone").Value);
        // email in the JWT should be the explicit parameter, not the passthrough claim
        Assert.Equal("actual@example.com",
            jwt.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.Email).Value);
    }

    [Fact]
    public void IsCoLoaded_IsReserved_PassthroughSkipsDifferentCasing()
    {
        // Guards against a case-sensitive caller producing a duplicate claim by including
        // both the canonical name (set by BuildAndSignToken as a base claim) and a variant
        // casing in the passthrough dict. ReservedClaimNames uses OrdinalIgnoreCase, so
        // the variant must be skipped rather than emitted as a second is_co_loaded claim.
        var claims = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [JwtClaimTypes.Ial] = "1",
            [JwtClaimTypes.IdProofingStatus] = "0",
            ["IS_CO_LOADED"] = "rogue"
        };

        var token = Service.BuildAndSignToken(Guid.NewGuid(), "user@example.com", claims);

        var jwt = ReadJwt(token);
        var isCoLoadedClaims = jwt.Claims.Where(c => c.Type == JwtClaimTypes.IsCoLoaded).ToList();
        Assert.Single(isCoLoadedClaims);
        Assert.Equal("false", isCoLoadedClaims[0].Value);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "IS_CO_LOADED"));
    }

    [Fact]
    public void IsCoLoaded_DefaultsToFalse_WhenMissingFromResolvedClaims()
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var token = Service.BuildAndSignToken(Guid.NewGuid(), "user@example.com", claims);

        var jwt = ReadJwt(token);
        Assert.Equal("false", jwt.Claims.First(c => c.Type == JwtClaimTypes.IsCoLoaded).Value);
    }
}
