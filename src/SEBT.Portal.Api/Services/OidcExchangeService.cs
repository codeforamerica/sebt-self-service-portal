using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SEBT.Portal.Api.Services;

/// <summary>
/// performs the OIDC token exchange and id_token verification entirely server-side.
/// Replaces the Next.js <c>/api/auth/oidc/callback/route.ts</c> — the client secret, JWKS
/// validation, and callback-token signing all happen in .NET now.
///
/// The service is stateless between requests (all flow state lives in the pre-auth session
/// store). Inject as scoped or transient.
/// </summary>
public interface IOidcExchangeService
{
    /// <summary>
    /// Exchanges an authorization code for an id_token, verifies it, and signs a short-lived
    /// callback token containing the IdP claims. Returns the callback token on success.
    /// </summary>
    /// <param name="code">Authorization code from PingOne redirect.</param>
    /// <param name="codeVerifier">PKCE code_verifier (from the pre-auth session, never from the browser).</param>
    /// <param name="redirectUri">redirect_uri that was sent in the authorization request.</param>
    /// <param name="isStepUp">True when this is a step-up (IAL1+) flow.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Signed callback token (short-lived JWT) on success.</returns>
    Task<OidcExchangeResult> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        bool isStepUp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the cached OIDC discovery document for the configured IdP. Returns the
    /// <see cref="OpenIdConnectConfiguration"/> containing endpoint URLs (authorization,
    /// token, userinfo), signing keys, and issuer metadata.
    /// </summary>
    /// <param name="isStepUp">True to use the step-up IdP configuration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<OpenIdConnectConfiguration> GetDiscoveryConfigAsync(
        bool isStepUp,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of the OIDC code exchange.</summary>
public sealed record OidcExchangeResult
{
    /// <summary>True when exchange + verification succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Signed callback token (short-lived JWT containing IdP claims). Null on failure.</summary>
    public string? CallbackToken { get; init; }

    /// <summary>Phone claim value extracted during the exchange (for diagnostic logging). Null when absent or on failure.</summary>
    public string? PhoneClaim { get; init; }

    /// <summary>Human-readable error message for the client. Null on success.</summary>
    public string? Error { get; init; }

    /// <summary>HTTP status code to return to the client. 200 on success.</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>Creates a successful result with the given callback token.</summary>
    public static OidcExchangeResult Ok(string callbackToken, string? phoneClaim = null) => new()
    {
        Success = true,
        CallbackToken = callbackToken,
        PhoneClaim = phoneClaim,
        StatusCode = 200
    };

    /// <summary>Creates a failed result with the given error message and HTTP status code.</summary>
    public static OidcExchangeResult Fail(string error, int statusCode = 400) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode
    };
}

/// <inheritdoc cref="IOidcExchangeService"/>
public sealed class OidcExchangeService : IOidcExchangeService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OidcExchangeService> _logger;

    /// <summary>strict exp check — ≤10 seconds clock skew tolerance.</summary>
    private static readonly TimeSpan IdTokenClockSkew = TimeSpan.FromSeconds(10);

    private const int CallbackTokenExpirySec = 300; // 5 minutes, matching the old Next.js value

    /// <summary>
    /// Standard OIDC/JWT and IdP-infrastructure claim names excluded when copying IdP
    /// claims into the callback token or portal JWT. Single source of truth — the
    /// controller's <c>CompleteLogin</c> references this same set.
    /// </summary>
    public static readonly HashSet<string> CommonOidcInfrastructureClaims = new(StringComparer.OrdinalIgnoreCase)
    {
        "iss", "aud", "iat", "exp", "nbf", "nonce", "at_hash", "c_hash",
        "auth_time", "acr", "amr", "azp", "sid", "jti",
        "env", "org", "p1.region"
    };

    /// <summary>
    /// Cached <see cref="ConfigurationManager{T}"/> instances keyed by discovery URL.
    /// <c>ConfigurationManager</c> is designed for singleton lifetime — it caches the
    /// discovery document and JWKS internally and refreshes them on a background timer.
    /// Creating one per request would defeat the cache and hit the IdP on every login.
    /// Uses a dedicated long-lived <see cref="HttpClient"/> per manager so the factory's
    /// handler recycling isn't bypassed by a static capture.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>
        DiscoveryManagers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient DiscoveryHttpClient = new(
        new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <inheritdoc cref="OidcExchangeService"/>
    public OidcExchangeService(
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<OidcExchangeService> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OpenIdConnectConfiguration> GetDiscoveryConfigAsync(
        bool isStepUp,
        CancellationToken cancellationToken = default)
    {
        var discoveryEndpoint = isStepUp
            ? _config["Oidc:StepUp:DiscoveryEndpoint"]
            : _config["Oidc:DiscoveryEndpoint"];

        if (string.IsNullOrEmpty(discoveryEndpoint))
        {
            throw new InvalidOperationException(
                $"OIDC discovery endpoint not configured (isStepUp={isStepUp}). " +
                "Set Oidc:DiscoveryEndpoint in appsettings.");
        }

        var configManager = DiscoveryManagers.GetOrAdd(discoveryEndpoint, url =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                url,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(DiscoveryHttpClient)));

        return await configManager.GetConfigurationAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<OidcExchangeResult> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        bool isStepUp,
        CancellationToken cancellationToken = default)
    {
        // --- Resolve per-flow config ---
        var clientId = isStepUp ? _config["Oidc:StepUp:ClientId"] : _config["Oidc:ClientId"];
        var clientSecret = isStepUp ? _config["Oidc:StepUp:ClientSecret"] : _config["Oidc:ClientSecret"];
        var signingKey = _config["Oidc:CompleteLoginSigningKey"];

        if (string.IsNullOrEmpty(clientId)
            || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(signingKey))
        {
            _logger.LogWarning("OIDC exchange: missing config (reason=oidc_not_configured, isStepUp={IsStepUp})", isStepUp);
            return OidcExchangeResult.Fail("OIDC not configured.", 503);
        }

        // --- Fetch discovery document (cached singleton per discovery URL) ---
        OpenIdConnectConfiguration oidcConfig;
        try
        {
            oidcConfig = await GetDiscoveryConfigAsync(isStepUp, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC exchange: failed to fetch discovery document (reason=discovery_failed)");
            return OidcExchangeResult.Fail("Failed to load OIDC discovery document.", 502);
        }

        if (string.IsNullOrEmpty(oidcConfig.TokenEndpoint))
        {
            return OidcExchangeResult.Fail("Invalid discovery document (missing token_endpoint).", 502);
        }

        // --- Exchange authorization code for tokens ---
        using var client = _httpFactory.CreateClient();
        using var tokenParams = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

        HttpResponseMessage tokenRes;
        try
        {
            tokenRes = await client.PostAsync(oidcConfig.TokenEndpoint, tokenParams, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC exchange: token request failed (reason=token_request_failed)");
            return OidcExchangeResult.Fail("Token exchange failed.");
        }

        var tokenBody = await tokenRes.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenRes.IsSuccessStatusCode)
        {
            // Log the raw IdP error for debugging but never forward it to the client
            // (error_description can contain internal IdP infrastructure details).
            try
            {
                using var doc = JsonDocument.Parse(tokenBody);
                var desc = doc.RootElement.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
                var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                _logger.LogError(
                    "OIDC exchange: token endpoint returned {StatusCode}, error={Error}, description={Description} (reason=token_exchange_rejected)",
                    (int)tokenRes.StatusCode, err, desc);
            }
            catch
            {
                _logger.LogError("OIDC exchange: token endpoint returned {StatusCode} (reason=token_exchange_rejected)", (int)tokenRes.StatusCode);
            }
            return OidcExchangeResult.Fail("Token exchange was rejected by the identity provider.");
        }

        // --- Parse token response ---
        string? idTokenRaw;
        string? accessToken;
        try
        {
            using var doc = JsonDocument.Parse(tokenBody);
            idTokenRaw = doc.RootElement.TryGetProperty("id_token", out var it) ? it.GetString() : null;
            accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC exchange: failed to parse token response (reason=token_parse_failed)");
            return OidcExchangeResult.Fail("Failed to parse token response.");
        }

        if (string.IsNullOrEmpty(idTokenRaw))
        {
            return OidcExchangeResult.Fail("No id_token in token response.");
        }

        // --- Verify id_token with JWKS + strict exp ---
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = oidcConfig.SigningKeys,
            ValidateIssuer = true,
            ValidIssuer = oidcConfig.Issuer,
            ValidateAudience = true,
            ValidAudiences = new[] { clientId },
            ValidateLifetime = true,
            ClockSkew = IdTokenClockSkew,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(idTokenRaw, validationParams, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogError("OIDC exchange: id_token expired beyond {Skew}s skew (reason=expired_token)", IdTokenClockSkew.TotalSeconds);
            return OidcExchangeResult.Fail("Id token has expired.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC exchange: id_token validation failed (reason=token_validation_failed)");
            return OidcExchangeResult.Fail("Id token validation failed.");
        }

        // --- Validate auth_time claim (OIDC Core §3.1.2.1: REQUIRED when max_age is sent) ---
        // We send max_age=0 in the authorize request, so the IdP must include auth_time
        // and it should reflect a fresh authentication. Log a warning if it's missing or
        // stale so we can observe IdP behavior before enforcing rejection.
        var authTimeClaim = principal.FindFirst("auth_time");
        if (authTimeClaim == null)
        {
            _logger.LogError(
                "OIDC exchange: id_token missing auth_time claim; IdP must include it when max_age is sent (reason=missing_auth_time, isStepUp={IsStepUp})",
                isStepUp);
        }
        else if (long.TryParse(authTimeClaim.Value, out var authTimeEpoch))
        {
            var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeEpoch);
            var authAge = DateTimeOffset.UtcNow - authTime;
            _logger.LogInformation(
                "OIDC exchange: auth_time={AuthTime}, age={AuthAgeSec}s (isStepUp={IsStepUp})",
                authTime, (int)authAge.TotalSeconds, isStepUp);
            if (authAge.TotalSeconds > 120)
            {
                _logger.LogError(
                    "OIDC exchange: auth_time is stale — user was authenticated {AuthAgeSec}s ago, expected fresh authentication with max_age=0 (reason=stale_auth_time, isStepUp={IsStepUp})",
                    (int)authAge.TotalSeconds, isStepUp);
            }
        }
        else
        {
            _logger.LogError(
                "OIDC exchange: auth_time claim present but not a valid Unix timestamp: {AuthTimeValue} (reason=invalid_auth_time, isStepUp={IsStepUp})",
                authTimeClaim.Value, isStepUp);
        }

        // --- Extract claims for the callback token ---
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
        {
            if (!CommonOidcInfrastructureClaims.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
                claims[claim.Type] = claim.Value;
        }

        // --- Fetch userinfo for profile claims (phone, givenName, etc.) ---
        if (!string.IsNullOrEmpty(oidcConfig.UserInfoEndpoint) && !string.IsNullOrEmpty(accessToken))
        {
            await EnrichClaimsFromUserInfo(oidcConfig.UserInfoEndpoint, accessToken, claims, cancellationToken);
        }

        // --- Verify we have at least sub or email ---
        if (!claims.ContainsKey("sub") && !claims.ContainsKey("email"))
        {
            _logger.LogError("OIDC exchange: id_token + userinfo had no sub or email (reason=missing_identity_claim)");
            return OidcExchangeResult.Fail("Callback token must contain an email or sub claim.");
        }

        // --- Sign the callback token with deployment-specific issuer/audience ---
        // Prevents cross-environment token confusion if the signing key were shared.
        var portalOrigin = _config["Oidc:CallbackRedirectUri"]?.TrimEnd('/') ?? "sebt-portal";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwtClaims = claims.Select(c => new Claim(c.Key, c.Value)).ToList();
        var callbackJwt = new JwtSecurityToken(
            issuer: portalOrigin,
            audience: portalOrigin,
            claims: jwtClaims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.AddSeconds(CallbackTokenExpirySec),
            signingCredentials: credentials);
        var callbackToken = handler.WriteToken(callbackJwt);

        // Surface the phone claim for diagnostic logging by the caller (masked before logging).
        claims.TryGetValue("phone", out var phoneClaim);
        if (phoneClaim == null)
            claims.TryGetValue("phone_number", out phoneClaim);

        _logger.LogInformation(
            "OIDC exchange succeeded: claim types={ClaimTypes} (reason=exchange_success, isStepUp={IsStepUp})",
            string.Join(", ", claims.Keys),
            isStepUp);

        return OidcExchangeResult.Ok(callbackToken, phoneClaim);
    }

    private async Task EnrichClaimsFromUserInfo(
        string userInfoEndpoint,
        string accessToken,
        Dictionary<string, string> claims,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var res = await client.GetAsync(userInfoEndpoint, cancellationToken);
            if (!res.IsSuccessStatusCode) return;

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            void TrySet(string jsonKey, string claimName)
            {
                if (doc.RootElement.TryGetProperty(jsonKey, out var val)
                    && val.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(val.GetString()))
                {
                    claims.TryAdd(claimName, val.GetString()!);
                }
            }

            TrySet("sub", "sub");
            TrySet("email", "email");
            // Some IdPs put email in preferred_username — only use it as email if it looks like one.
            if (!claims.ContainsKey("email")
                && doc.RootElement.TryGetProperty("preferred_username", out var pu)
                && pu.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(pu.GetString())
                && pu.GetString()!.Contains('@'))
            {
                claims.TryAdd("email", pu.GetString()!);
            }
            TrySet("phone", "phone");
            TrySet("phone_number", "phone_number");
            TrySet("given_name", "givenName");
            TrySet("givenName", "givenName");
            TrySet("family_name", "familyName");
            TrySet("familyName", "familyName");
            TrySet("name", "name");
        }
        catch (Exception ex)
        {
            // Userinfo is best-effort; id_token claims are already captured.
            _logger.LogInformation(ex, "OIDC exchange: userinfo fetch failed (non-fatal)");
        }
    }
}
