using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Unit coverage for the OIDC token exchange and claim enrichment.
///
/// Drives the real <see cref="OidcExchangeService"/> with no AspNetCore host and no live IdP:
/// the discovery document + JWKS are served by a local HTTP stand-in signed with an RSA key we
/// control, and the token + userinfo HTTP calls are mocked through <see cref="IHttpClientFactory"/>.
/// <see cref="OidcExchangeService"/>.EnrichClaimsFromUserInfo is private, so it is exercised
/// through <c>ExchangeCodeAsync</c>. End-to-end coverage of the controller callback is left to E2E.
/// </summary>
public class OidcExchangeServiceExchangeTests
{
    private const string ClientId = "test-client";
    private const string ClientSecret = "test-secret";

    // HMAC-SHA256 signing key for the callback token — must be at least 256 bits.
    private const string CallbackSigningKey = "callback-signing-key-0123456789-abcdefghijklmnop";
    private const string RedirectUri = "https://portal.example.com/oidc/callback";

    // ── Happy path ──

    [Fact]
    public async Task ExchangeCodeAsync_valid_code_returns_callback_token_with_id_token_and_userinfo_claims()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            new Claim("sub", "user-123"),
            new Claim("email", "user@example.com"));
        var handler = new RoutingHandler()
            .Token(TokenOk(idToken, accessToken: "access-abc"))
            .UserInfo(Json(HttpStatusCode.OK,
                """{"given_name":"Alex","family_name":"Rivera","name":"Alex Rivera","phone":"202-555-0100"}"""));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Error);
        Assert.NotNull(result.CallbackToken);
        Assert.Equal("202-555-0100", result.PhoneClaim);

        var claims = DecodeCallbackToken(result.CallbackToken!);
        Assert.Equal("user-123", claims["sub"]);
        Assert.Equal("user@example.com", claims["email"]);
        Assert.Equal("Alex", claims["givenName"]);   // enriched from userinfo
        Assert.Equal("Rivera", claims["familyName"]);
        Assert.Equal("Alex Rivera", claims["name"]);
    }

    [Fact]
    public async Task ExchangeCodeAsync_step_up_flow_resolves_step_up_configuration_and_succeeds()
    {
        // isStepUp=true routes clientId/secret/discovery through the Oidc:StepUp:* config section.
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            new Claim("sub", "user-123"),
            new Claim("email", "user@example.com"));
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var service = CreateService(idp.DiscoveryUrl, handler, isStepUp: true);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: true);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        var claims = DecodeCallbackToken(result.CallbackToken!);
        Assert.Equal("user-123", claims["sub"]);
        Assert.Equal("user@example.com", claims["email"]);
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_503_when_step_up_client_credentials_not_configured()
    {
        // Step-up flow with only the non-step-up credentials set → step-up clientId/secret resolve empty.
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var service = CreateService(idp.DiscoveryUrl, new RoutingHandler(), isStepUp: false);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: true);

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Contains("not configured", result.Error);
    }

    // ── Configuration / discovery failures ──

    [Fact]
    public async Task ExchangeCodeAsync_returns_503_when_signing_key_not_configured()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        // Omit the callback signing key — the service should fail before any network call.
        var service = CreateService(idp.DiscoveryUrl, new RoutingHandler(), configured: false);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Contains("not configured", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_502_when_discovery_document_cannot_be_loaded()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        // Point discovery at a path the stand-in returns 404 for → ConfigurationManager throws.
        var service = CreateService($"{idp.BaseUrl}/unreachable-{Guid.NewGuid():N}", new RoutingHandler());

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("discovery", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_returns_502_when_discovery_missing_token_endpoint()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId, includeTokenEndpoint: false);
        var service = CreateService(idp.DiscoveryUrl, new RoutingHandler());

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("token_endpoint", result.Error);
    }

    // ── Token endpoint failures ──

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_token_request_throws()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var handler = new RoutingHandler().TokenThrows(new HttpRequestException("connection reset"));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("Token exchange failed", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_token_endpoint_rejects_the_code()
    {
        // Invalid/expired authorization code — the IdP responds 400 invalid_grant.
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var handler = new RoutingHandler().Token(Json(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"Code not valid"}"""));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("bad-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("rejected by the identity provider", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_token_response_is_not_json()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var handler = new RoutingHandler().Token(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>not json</html>", Encoding.UTF8, "text/html")
        });
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("parse token response", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_token_response_has_no_id_token()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var handler = new RoutingHandler().Token(Json(HttpStatusCode.OK,
            """{"access_token":"access-abc","token_type":"Bearer"}"""));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("No id_token", result.Error);
    }

    // ── id_token verification failures ──

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_id_token_is_expired()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            claims: new[] { new Claim("sub", "user-123") },
            expires: DateTime.UtcNow.AddMinutes(-10));
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("expired", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_id_token_issuer_is_wrong()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            claims: new[] { new Claim("sub", "user-123") },
            issuerOverride: "https://attacker.example.com");
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("validation failed", result.Error);
    }

    [Fact]
    public async Task ExchangeCodeAsync_fails_when_no_sub_or_email_claim_can_be_resolved()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        // id_token carries no sub/email, and with no access_token userinfo is not consulted.
        var idToken = idp.SignIdToken(new Claim("given_name", "Alex"));
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.False(result.Success);
        Assert.Contains("email or sub", result.Error);
    }

    // ── auth_time diagnostics ──
    // The service sends max_age=0, so auth_time is REQUIRED and should be fresh. Missing, stale,
    // or non-numeric auth_time is logged as an error but never fails the exchange (observe-only).

    [Fact]
    public async Task ExchangeCodeAsync_succeeds_and_logs_error_when_auth_time_is_missing()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            new[] { new Claim("sub", "user-123"), new Claim("email", "user@example.com") },
            includeAuthTime: false);
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var logger = new CapturingLogger<OidcExchangeService>();
        var service = CreateService(idp.DiscoveryUrl, handler, logger: logger);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("missing_auth_time"));
    }

    [Fact]
    public async Task ExchangeCodeAsync_succeeds_and_logs_error_when_auth_time_is_stale()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var staleAuthTime = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var idToken = idp.SignIdToken(
            new[] { new Claim("sub", "user-123"), new Claim("email", "user@example.com") },
            authTimeValue: staleAuthTime);
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var logger = new CapturingLogger<OidcExchangeService>();
        var service = CreateService(idp.DiscoveryUrl, handler, logger: logger);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("stale_auth_time"));
    }

    [Fact]
    public async Task ExchangeCodeAsync_succeeds_and_logs_error_when_auth_time_is_not_a_valid_timestamp()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            new[] { new Claim("sub", "user-123"), new Claim("email", "user@example.com") },
            authTimeValue: "not-a-timestamp");
        var handler = new RoutingHandler().Token(TokenOk(idToken, accessToken: null));
        var logger = new CapturingLogger<OidcExchangeService>();
        var service = CreateService(idp.DiscoveryUrl, handler, logger: logger);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("invalid_auth_time"));
    }

    // ── EnrichClaimsFromUserInfo ──

    [Fact]
    public async Task ExchangeCodeAsync_recovers_email_from_userinfo_when_id_token_lacks_identity()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(new Claim("given_name", "Alex")); // no sub/email in id_token
        var handler = new RoutingHandler()
            .Token(TokenOk(idToken, accessToken: "access-abc"))
            .UserInfo(Json(HttpStatusCode.OK, """{"email":"recovered@example.com"}"""));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Equal("recovered@example.com", DecodeCallbackToken(result.CallbackToken!)["email"]);
    }

    [Fact]
    public async Task ExchangeCodeAsync_recovers_email_from_preferred_username_when_it_looks_like_an_email()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(new Claim("sub", "user-123")); // no email
        var handler = new RoutingHandler()
            .Token(TokenOk(idToken, accessToken: "access-abc"))
            .UserInfo(Json(HttpStatusCode.OK, """{"preferred_username":"user@example.com"}"""));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Equal("user@example.com", DecodeCallbackToken(result.CallbackToken!)["email"]);
    }

    [Fact]
    public async Task ExchangeCodeAsync_succeeds_when_userinfo_returns_non_success_status()
    {
        // Userinfo is best-effort — a failed fetch must not fail the exchange.
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            new Claim("sub", "user-123"),
            new Claim("email", "user@example.com"));
        var handler = new RoutingHandler()
            .Token(TokenOk(idToken, accessToken: "access-abc"))
            .UserInfo(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        var claims = DecodeCallbackToken(result.CallbackToken!);
        Assert.Equal("user-123", claims["sub"]);
        Assert.False(claims.ContainsKey("givenName")); // nothing enriched
    }

    [Fact]
    public async Task ExchangeCodeAsync_succeeds_when_userinfo_returns_malformed_json()
    {
        await using var idp = SigningDiscoveryServer.Start(audience: ClientId);
        var idToken = idp.SignIdToken(
            new Claim("sub", "user-123"),
            new Claim("email", "user@example.com"));
        var handler = new RoutingHandler()
            .Token(TokenOk(idToken, accessToken: "access-abc"))
            .UserInfo(Json(HttpStatusCode.OK, "this is not json"));
        var service = CreateService(idp.DiscoveryUrl, handler);

        var result = await service.ExchangeCodeAsync("auth-code", "verifier", RedirectUri, isStepUp: false);

        Assert.True(result.Success);
        Assert.Equal("user-123", DecodeCallbackToken(result.CallbackToken!)["sub"]);
    }

    // ── Test infrastructure ──

    private static OidcExchangeService CreateService(
        string discoveryUrl,
        RoutingHandler handler,
        bool configured = true,
        bool isStepUp = false,
        ILogger<OidcExchangeService>? logger = null)
    {
        // The service resolves the discovery endpoint and client credentials from the step-up
        // config section when isStepUp is true; the callback signing key is shared across flows.
        var prefix = isStepUp ? "Oidc:StepUp:" : "Oidc:";
        var settings = new Dictionary<string, string?>
        {
            [$"{prefix}DiscoveryEndpoint"] = discoveryUrl,
            ["Oidc:CallbackRedirectUri"] = "https://portal.example.com"
        };
        if (configured)
        {
            settings[$"{prefix}ClientId"] = ClientId;
            settings[$"{prefix}ClientSecret"] = ClientSecret;
            settings["Oidc:CompleteLoginSigningKey"] = CallbackSigningKey;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var factory = Substitute.For<IHttpClientFactory>();
        // Fresh HttpClient per call (token + userinfo), sharing one handler that is not disposed
        // when a client is disposed — the service wraps each client in a `using`.
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));

        return new OidcExchangeService(
            config,
            factory,
            logger ?? NullLogger<OidcExchangeService>.Instance,
            Substitute.For<IOidcCallbackFailureLogger>());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage TokenOk(string idToken, string? accessToken)
    {
        var payload = new Dictionary<string, object> { ["id_token"] = idToken, ["token_type"] = "Bearer" };
        if (accessToken != null)
        {
            payload["access_token"] = accessToken;
        }

        return Json(HttpStatusCode.OK, JsonSerializer.Serialize(payload));
    }

    private static IReadOnlyDictionary<string, string> DecodeCallbackToken(string jwt)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var claims = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var claim in token.Claims)
        {
            claims.TryAdd(claim.Type, claim.Value);
        }

        return claims;
    }

    /// <summary>
    /// Minimal <see cref="ILogger{T}"/> that records each entry's level and rendered message, so the
    /// auth_time diagnostics (pure logging, no effect on the result) can be asserted.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Routes the token and userinfo HTTP calls to canned responses (or exceptions). Discovery +
    /// JWKS never reach this handler — those go through the service's own long-lived discovery
    /// client to <see cref="SigningDiscoveryServer"/>.
    /// </summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage>? _token;
        private Func<HttpResponseMessage>? _userInfo;

        public RoutingHandler Token(HttpResponseMessage response)
        {
            _token = () => response;
            return this;
        }

        public RoutingHandler TokenThrows(Exception ex)
        {
            _token = () => throw ex;
            return this;
        }

        public RoutingHandler UserInfo(HttpResponseMessage response)
        {
            _userInfo = () => response;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var responder =
                path.EndsWith("/token", StringComparison.Ordinal) ? _token
                : path.EndsWith("/userinfo", StringComparison.Ordinal) ? _userInfo
                : null;

            if (responder is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            try
            {
                return Task.FromResult(responder());
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }

    /// <summary>
    /// Local HTTP OIDC discovery + JWKS stand-in that signs id_tokens with an RSA key we own, so
    /// the service's id_token verification (JWKS + issuer + audience) exercises the real path
    /// without a live IdP. Each instance uses a unique realm so the service's static
    /// discovery-config cache never collides across tests.
    /// </summary>
    private sealed class SigningDiscoveryServer : IAsyncDisposable
    {
        private const string Kid = "test-signing-key";

        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private readonly RSA _rsa;
        private readonly string _audience;
        private readonly bool _includeTokenEndpoint;
        private readonly bool _includeUserInfoEndpoint;

        public string BaseUrl { get; }
        public string Issuer { get; }
        public string DiscoveryUrl { get; }
        public string TokenEndpoint { get; }
        public string UserInfoEndpoint { get; }

        private SigningDiscoveryServer(
            HttpListener listener, string baseUrl, string realm, RSA rsa,
            string audience, bool includeTokenEndpoint, bool includeUserInfoEndpoint)
        {
            _listener = listener;
            _rsa = rsa;
            _audience = audience;
            _includeTokenEndpoint = includeTokenEndpoint;
            _includeUserInfoEndpoint = includeUserInfoEndpoint;
            BaseUrl = baseUrl;
            Issuer = $"{baseUrl}/realms/{realm}";
            DiscoveryUrl = $"{Issuer}/.well-known/openid-configuration";
            TokenEndpoint = $"{Issuer}/token";
            UserInfoEndpoint = $"{Issuer}/userinfo";
            _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public static SigningDiscoveryServer Start(
            string audience, bool includeTokenEndpoint = true, bool includeUserInfoEndpoint = true)
        {
            var realm = "sebt-" + Guid.NewGuid().ToString("N");
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var port = Random.Shared.Next(20_000, 40_000);
                var baseUrl = $"http://127.0.0.1:{port}";
                var listener = new HttpListener();
                listener.Prefixes.Add($"{baseUrl}/");
                try
                {
                    listener.Start();
                    return new SigningDiscoveryServer(
                        listener, baseUrl, realm, RSA.Create(2048),
                        audience, includeTokenEndpoint, includeUserInfoEndpoint);
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                }
            }

            throw new InvalidOperationException("Could not bind a local HTTP listener for OIDC exchange tests.");
        }

        /// <summary>Signs an id_token with the server's RSA key. Overrides let failure tests
        /// produce an expired token or one from the wrong issuer.</summary>
        public string SignIdToken(
            IEnumerable<Claim> claims,
            string? issuerOverride = null,
            DateTime? expires = null,
            bool includeAuthTime = true,
            string? authTimeValue = null)
        {
            var payloadClaims = new List<Claim>(claims);
            if (includeAuthTime)
            {
                // auth_time is REQUIRED by the service when max_age is sent; default to a fresh value so
                // the happy path doesn't log a missing/stale-auth_time warning. Excluded from copied claims.
                // authTimeValue overrides drive the auth_time diagnostics tests (stale / invalid).
                payloadClaims.Add(new Claim(
                    "auth_time", authTimeValue ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
            }

            var key = new RsaSecurityKey(_rsa) { KeyId = Kid };
            var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
            // notBefore is anchored 6 minutes before expiry so an overridden (past) expiry still
            // satisfies notBefore < expires; for the default expiry this lands at ~1 minute ago.
            var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
            var token = new JwtSecurityToken(
                issuer: issuerOverride ?? Issuer,
                audience: _audience,
                claims: payloadClaims,
                notBefore: expiresAt.AddMinutes(-6),
                expires: expiresAt,
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string SignIdToken(params Claim[] claims) => SignIdToken((IEnumerable<Claim>)claims);

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => WriteResponse(context), CancellationToken.None);
            }
        }

        private void WriteResponse(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            string body;

            if (path.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
            {
                var discovery = new Dictionary<string, object>
                {
                    ["issuer"] = Issuer,
                    ["authorization_endpoint"] = $"{Issuer}/auth",
                    ["jwks_uri"] = $"{Issuer}/certs",
                    ["response_types_supported"] = new[] { "code" },
                    ["subject_types_supported"] = new[] { "public" },
                    ["id_token_signing_alg_values_supported"] = new[] { "RS256" }
                };
                if (_includeTokenEndpoint)
                {
                    discovery["token_endpoint"] = TokenEndpoint;
                }

                if (_includeUserInfoEndpoint)
                {
                    discovery["userinfo_endpoint"] = UserInfoEndpoint;
                }

                body = JsonSerializer.Serialize(discovery);
            }
            else if (path.EndsWith("/certs", StringComparison.Ordinal))
            {
                var parameters = _rsa.ExportParameters(false);
                var jwk = new Dictionary<string, object>
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = Kid,
                    ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
                    ["e"] = Base64UrlEncoder.Encode(parameters.Exponent)
                };
                body = JsonSerializer.Serialize(new Dictionary<string, object> { ["keys"] = new[] { jwk } });
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            _listener.Close();
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // Listener shutdown can leave GetContextAsync hanging briefly.
            }

            _cts.Dispose();
            _rsa.Dispose();
        }
    }
}
