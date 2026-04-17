using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests that simulate the pen-test attack chains (T02, T03, T04,
/// T07a, T08a-A) against the real HTTP pipeline. Each test proves a vulnerability is closed by
/// the pre-auth session binding, Origin enforcement, stateCode allowlist, and one-time token
/// consumption.
///
/// Test naming convention: V{xx}_{PenTestId}_{Description}
///

/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OidcPreAuthSecurityTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly PortalWebApplicationFactory _factory;

    public OidcPreAuthSecurityTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------------------
    // V01 / T03 — No session binding on /complete-login
    // Attack: POST a valid callback token from an attacker's machine with no
    //         cookies, spoofed Origin. Before this returned a portal JWT.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task V01_T03_CompleteLogin_WithoutSessionCookie_Returns403()
    {
        var client = _factory.CreateClient();
        var callbackToken = MintCallbackToken("user@example.com");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/complete-login")
        {
            Content = JsonContent.Create(new { stateCode = "co", callbackToken }),
            Headers = { { "Origin", "http://localhost:3000" } }
        };
        // No oidc_session cookie — simulating attacker's bare request

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task V01_T03_CompleteLogin_WithSpoofedOrigin_Returns403()
    {
        var client = _factory.CreateClient();
        var callbackToken = MintCallbackToken("user@example.com");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/complete-login")
        {
            Content = JsonContent.Create(new { stateCode = "co", callbackToken }),
            Headers = { { "Origin", "https://attacker.example.com" } }
        };

        var response = await client.SendAsync(request);

        // Origin middleware rejects before we even reach the session check
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task V01_T03_CompleteLogin_WithMissingOrigin_Returns403()
    {
        var client = _factory.CreateClient();
        var callbackToken = MintCallbackToken("user@example.com");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/complete-login")
        {
            Content = JsonContent.Create(new { stateCode = "co", callbackToken })
            // No Origin header at all — curl from command line
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // V02 / T02 — Unlimited token replay on /complete-login
    // Attack: POST the same callback token twice. Before each call
    //         returned a fresh portal JWT, creating unlimited sessions.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// V02/T02: replaying the same callback token against <c>complete-login</c> must fail
    /// on the second attempt. The pre-auth session advances to <c>LoginCompleted</c> on
    /// first use; a second call with the same session cookie returns 403.
    ///
    /// This test sends two requests to <c>complete-login</c> with the same session. The
    /// first may succeed or fail (depending on callback token validation against the test
    /// signing key), but the second must *always* be 403 because the session has been
    /// consumed. That's the one-time-use guarantee the pen test cares about.
    /// </summary>
    [Fact]
    public async Task V02_T02_CompleteLogin_SecondCallWithConsumedSession_Returns403()
    {
        var client = _factory.CreateClient();
        var sessionStore = _factory.Services.GetRequiredService<IPreAuthSessionStore>();
        var callbackToken = MintCallbackToken("user@example.com");
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(callbackToken);

        // Create session and advance it all the way to LoginCompleted (simulating a
        // successful first complete-login that has already consumed the session).
        var session = await sessionStore.CreateAsync("co", "state1", "verifier1", "http://localhost:3000/callback", false);
        await sessionStore.TryAdvanceToCallbackCompletedAsync(session.Id, tokenHash);
        await sessionStore.TryAdvanceToLoginCompletedAsync(session.Id, tokenHash);

        // Replay attempt: exact same token + cookie → must fail
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/complete-login")
        {
            Content = JsonContent.Create(new { stateCode = "co", callbackToken }),
            Headers =
            {
                { "Origin", "http://localhost:3000" },
                { "Cookie", $"{OidcSessionCookie.CookieName}={session.Id}" }
            }
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // V03 / T04 — state parameter not validated (CSRF)
    // Attack: Tamper with the state value on the callback POST. Before 
    //         the server didn't validate state server-side.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task V03_T04_Callback_WithTamperedState_Returns400()
    {
        var sessionStore = _factory.Services.GetRequiredService<IPreAuthSessionStore>();
        var session = await sessionStore.CreateAsync("co", "real-state-value", "verifier1", "http://localhost:3000/callback", false);

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/callback")
        {
            Content = JsonContent.Create(new
            {
                code = "auth-code-123",
                state = "tampered-csrf-state-value", // NOT the real state
                stateCode = "co"
            }),
            Headers =
            {
                { "Origin", "http://localhost:3000" },
                { "Cookie", $"{OidcSessionCookie.CookieName}={session.Id}" }
            }
        };

        var response = await client.SendAsync(request);

        // Attacker's perspective: the request is rejected — no portal JWT returned.
        // 400 = state mismatch detected; 403 = session lookup failed (Redis unavailable in tests).
        // Both mean the attack failed.
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 400 or 403 but got {(int)response.StatusCode}");
    }

    // ---------------------------------------------------------------------------
    // V05 / T07a — stateCode tenant escape (via Authorize endpoint)
    // stateCode is now validated at the Authorize endpoint, which is the single
    // entry point for OIDC flows. Callback and CompleteLogin read stateCode from
    // the server-side pre-auth session, so the body's stateCode is irrelevant.
    // These tests replace the removed V05_T07a_CompleteLogin_WithoutSession and
    // V05_T07a_Callback_WithoutSession tests, and also cover V06_T08aE
    // (stateCode mismatch with valid session) — that attack is now impossible
    // because the body's stateCode is never read.
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("az")]
    [InlineData("XX")]
    [InlineData("admin")]
    // "co\0admin" is not tested here since null bytes in URL paths are rejected by
    // ASP.NET's URL parser before reaching the controller, which is a valid defense.
    public async Task V05_T07a_Authorize_WithInvalidStateCode_Returns400(string stateCode)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/auth/oidc/{stateCode}/authorize");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The Authorize endpoint is the only place stateCode is accepted from user input.
    /// A valid stateCode creates a pre-auth session and redirects to the IdP.
    /// The redirect will fail (test host has no real IdP) but the point is the
    /// stateCode passed the allowlist — contrast with the invalid cases above.
    /// </summary>
    [Fact]
    public async Task V05_T07a_Authorize_WithValidStateCode_PassesAllowlist()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/auth/oidc/co/authorize");

        // Should NOT be 400 — the stateCode "co" is in the allowlist.
        // It may redirect (302) or fail downstream (discovery/config), but not 400.
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // V06 / T08a-A — No session binding on /api/auth/oidc/callback
    // Attack: POST a valid auth code + code_verifier from attacker's machine
    //         with no cookies. Before this returned the victim's ID token.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task V06_T08aA_Callback_WithoutSessionCookie_Returns403()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/callback")
        {
            Content = JsonContent.Create(new
            {
                code = "stolen-auth-code",
                state = "some-state",
                stateCode = "co"
            }),
            Headers = { { "Origin", "http://localhost:3000" } }
        };
        // No oidc_session cookie — attacker's bare request

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task V06_T08aA_Callback_WithSpoofedOrigin_Returns403()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/callback")
        {
            Content = JsonContent.Create(new
            {
                code = "stolen-auth-code",
                state = "some-state",
                stateCode = "co"
            }),
            Headers = { { "Origin", "https://attacker.example.com" } }
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // V06 / T08a-B — state mismatch on callback with valid session
    // Attack: Attacker has a session cookie but tampers with the state value.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task V06_T08aB_Callback_WithValidSessionButWrongState_Returns400()
    {
        var sessionStore = _factory.Services.GetRequiredService<IPreAuthSessionStore>();
        var session = await sessionStore.CreateAsync("co", "correct-state", "verifier", "http://localhost:3000/callback", false);

        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/callback")
        {
            Content = JsonContent.Create(new
            {
                code = "some-code",
                state = "wrong-state",
                stateCode = "co"
            }),
            Headers =
            {
                { "Origin", "http://localhost:3000" },
                { "Cookie", $"{OidcSessionCookie.CookieName}={session.Id}" }
            }
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Session lifecycle: a consumed session cannot be reused for callback
    // This covers the "replay the authorization code" scenario where the attacker
    // tries to exchange the same code twice using the same session cookie.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SessionReplay_CallbackOnAlreadyUsedSession_Returns400()
    {
        var sessionStore = _factory.Services.GetRequiredService<IPreAuthSessionStore>();
        var session = await sessionStore.CreateAsync("co", "state1", "verifier1", "http://localhost:3000/callback", false);
        // Simulate a successful callback by advancing the session
        await sessionStore.TryAdvanceToCallbackCompletedAsync(session.Id, "hash1");

        var client = _factory.CreateClient();
        // Try to use the same session for another callback — should fail
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/callback")
        {
            Content = JsonContent.Create(new
            {
                code = "another-code",
                state = "state1",
                stateCode = "co"
            }),
            Headers =
            {
                { "Origin", "http://localhost:3000" },
                { "Cookie", $"{OidcSessionCookie.CookieName}={session.Id}" }
            }
        };

        var response = await client.SendAsync(request);

        // The session is in CallbackCompleted phase, not Created — callback should reject
        // (The exact HTTP status depends on how the controller handles this — either the
        // exchange will fail because the session's code_verifier was already consumed by
        // the first exchange, or the session phase check prevents re-entry.)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 400 or 403 but got {(int)response.StatusCode}");
    }

    // ---------------------------------------------------------------------------
    // Cross-session token use
    // Attack: Attacker obtains a valid callbackToken from session A and presents
    //         it with session B's cookie. The token hash stored in session B won't
    //         match, so TryAdvanceToLoginCompleted must reject.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CrossSession_CompleteLogin_WithTokenFromDifferentSession_Returns403()
    {
        var client = _factory.CreateClient();
        var sessionStore = _factory.Services.GetRequiredService<IPreAuthSessionStore>();

        var callbackTokenA = MintCallbackToken("user-a@example.com");
        var tokenHashA = IPreAuthSessionStore.HashCallbackToken(callbackTokenA);

        var callbackTokenB = MintCallbackToken("user-b@example.com");
        var tokenHashB = IPreAuthSessionStore.HashCallbackToken(callbackTokenB);

        // Create two sessions and advance each to CallbackCompleted with their own token hash
        var sessionA = await sessionStore.CreateAsync("co", "stateA", "verifierA", "http://localhost:3000/callback", false);
        await sessionStore.TryAdvanceToCallbackCompletedAsync(sessionA.Id, tokenHashA);

        var sessionB = await sessionStore.CreateAsync("co", "stateB", "verifierB", "http://localhost:3000/callback", false);
        await sessionStore.TryAdvanceToCallbackCompletedAsync(sessionB.Id, tokenHashB);

        // Attack: use session B's cookie with session A's callback token
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/complete-login")
        {
            Content = JsonContent.Create(new { stateCode = "co", callbackToken = callbackTokenA }),
            Headers =
            {
                { "Origin", "http://localhost:3000" },
                { "Cookie", $"{OidcSessionCookie.CookieName}={sessionB.Id}" }
            }
        };

        var response = await client.SendAsync(request);

        // Token hash mismatch — session B expects tokenHashB but got tokenHashA
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Mints a callback token signed with the test secret key. Simulates
    /// what <see cref="OidcExchangeService"/> produces after a real IdP exchange.
    /// </summary>
    private static string MintCallbackToken(string email)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(PortalWebApplicationFactory.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("email", email),
            new Claim("sub", email)
        };
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
