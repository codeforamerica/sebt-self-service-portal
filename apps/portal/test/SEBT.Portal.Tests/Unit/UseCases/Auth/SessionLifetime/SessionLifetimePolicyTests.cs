using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.UseCases.Auth.SessionLifetime;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth.SessionLifetime;

public class SessionLifetimePolicyTests
{
    private const int CapMinutes = 60;
    private static readonly DateTimeOffset Now = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static SessionLifetimePolicy CreatePolicy(FakeTimeProvider timeProvider)
    {
        var settings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 15,
            AbsoluteExpirationMinutes = CapMinutes
        });
        return new SessionLifetimePolicy(settings, timeProvider);
    }

    private static ClaimsPrincipal PrincipalWith(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.type, c.value)));
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Evaluate_ReturnsValid_WhenAuthTimeIsRecent()
    {
        var time = new FakeTimeProvider(Now);
        var policy = CreatePolicy(time);
        var authTime = Now.AddMinutes(-(CapMinutes - 1)).ToUnixTimeSeconds();
        var principal = PrincipalWith(("auth_time", authTime.ToString()));

        Assert.Equal(SessionLifetimePolicy.Outcome.Valid, policy.Evaluate(principal));
    }

    [Fact]
    public void Evaluate_ReturnsMissingAuthTime_WhenClaimAbsent()
    {
        // Pre-existing sessions issued before auth_time stamping land here.
        var policy = CreatePolicy(new FakeTimeProvider(Now));
        var principal = PrincipalWith(("sub", Guid.NewGuid().ToString()));

        Assert.Equal(SessionLifetimePolicy.Outcome.MissingAuthTime, policy.Evaluate(principal));
    }

    [Fact]
    public void Evaluate_ReturnsMissingAuthTime_WhenClaimIsNotNumeric()
    {
        // Defensive: a non-numeric value is treated as if absent rather than crashing.
        var policy = CreatePolicy(new FakeTimeProvider(Now));
        var principal = PrincipalWith(("auth_time", "not-a-number"));

        Assert.Equal(SessionLifetimePolicy.Outcome.MissingAuthTime, policy.Evaluate(principal));
    }

    [Fact]
    public void Evaluate_ReturnsExpired_AtCapBoundary()
    {
        // ageSeconds == capSeconds → expired. Documents the >= boundary.
        var time = new FakeTimeProvider(Now);
        var policy = CreatePolicy(time);
        var authTime = Now.AddMinutes(-CapMinutes).ToUnixTimeSeconds();
        var principal = PrincipalWith(("auth_time", authTime.ToString()));

        Assert.Equal(SessionLifetimePolicy.Outcome.Expired, policy.Evaluate(principal));
    }

    [Fact]
    public void Evaluate_ReturnsExpired_PastCap()
    {
        var time = new FakeTimeProvider(Now);
        var policy = CreatePolicy(time);
        var authTime = Now.AddMinutes(-(CapMinutes + 5)).ToUnixTimeSeconds();
        var principal = PrincipalWith(("auth_time", authTime.ToString()));

        Assert.Equal(SessionLifetimePolicy.Outcome.Expired, policy.Evaluate(principal));
    }
}
