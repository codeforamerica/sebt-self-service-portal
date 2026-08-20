using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Api.Services;

/// <inheritdoc cref="IOidcExchangeService"/>
public sealed class OidcExchangeService : IOidcExchangeService
{
    private readonly IOptionsSnapshot<OidcSettings> _oidcSettings;
    private readonly IOptionsSnapshot<OidcStepUpSettings> _oidcStepUpSettings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OidcExchangeService> _logger;
    private readonly IOidcCallbackFailureLogger _callbackFailureLogger;

    /// <summary>strict exp check — ≤10 seconds clock skew tolerance.</summary>
    private static readonly TimeSpan IdTokenClockSkew = TimeSpan.FromSeconds(10);

    private const int CallbackTokenExpirySec = 300; // 5 minutes, matching the old Next.js value

    /// <summary>
    /// Standard OIDC/JWT and IdP-infrastructure claim names excluded when copying IdP
    /// claims into the callback token or portal JWT. Single source of truth — the
    /// controller's <c>CompleteLogin</c> references this same set.
    /// </summary>
    private static readonly HashSet<string> CommonOidcInfrastructureClaims = new(StringComparer.OrdinalIgnoreCase)
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
        IOptionsSnapshot<OidcSettings> oidcSettings,
        IOptionsSnapshot<OidcStepUpSettings> oidcStepUpSettings,
        IHttpClientFactory httpFactory,
        ILogger<OidcExchangeService> logger,
        IOidcCallbackFailureLogger callbackFailureLogger)
    {
        _oidcSettings = oidcSettings;
        _oidcStepUpSettings = oidcStepUpSettings;
        _httpFactory = httpFactory;
        _logger = logger;
        _callbackFailureLogger = callbackFailureLogger;
    }

    /// <inheritdoc/>
    public async Task<OpenIdConnectConfiguration> GetDiscoveryConfigAsync(
        bool isStepUp,
        CancellationToken cancellationToken = default)
    {
        var discoveryEndpoint = GetSettings(isStepUp).DiscoveryEndpoint;

        if (string.IsNullOrEmpty(discoveryEndpoint))
        {
            throw new InvalidOperationException(
                $"OIDC discovery endpoint not configured (isStepUp={isStepUp}). " +
                "Set Oidc:DiscoveryEndpoint in appsettings.");
        }

        var configManager = DiscoveryManagers.GetOrAdd(discoveryEndpoint, url =>
        {
            // HttpDocumentRetriever defaults to HTTPS-only. Local Keycloak (and similar
            // dev IdPs) serve discovery over http://localhost; allow that when the
            // configured discovery URL is itself http.
            var retriever = new HttpDocumentRetriever(DiscoveryHttpClient)
            {
                RequireHttps = DiscoveryRequiresHttps(url)
            };
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                url,
                new OpenIdConnectConfigurationRetriever(),
                retriever);
        });

        return await configManager.GetConfigurationAsync(cancellationToken);
    }

    /// <summary>
    /// Whether discovery document retrieval must use HTTPS for the given endpoint URL.
    /// True for <c>https://</c> IdPs; false for local <c>http://</c> stand-ins such as Keycloak.
    /// </summary>
    internal static bool DiscoveryRequiresHttps(string discoveryEndpoint) =>
        discoveryEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<OidcExchangeResult> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        bool isStepUp,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        // --- Resolve per-flow config ---
        var settings = GetSettings(isStepUp);
        var clientId = settings.ClientId;
        var clientSecret = settings.ClientSecret;

        // Signing key is portal-level (not IdP-per-flow) — always read from the base Oidc section.
        var signingKey = _oidcSettings.Value.CompleteLoginSigningKey;

        if (string.IsNullOrEmpty(clientId)
            || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(signingKey))
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "oidc_not_configured",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status503ServiceUnavailable
            });
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
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "discovery_failed",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status502BadGateway,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail("Failed to load OIDC discovery document.", 502);
        }

        if (string.IsNullOrEmpty(oidcConfig.TokenEndpoint))
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "discovery_invalid",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status502BadGateway
            });
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
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "token_request_failed",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail("Token exchange failed.");
        }

        var tokenBody = await tokenRes.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenRes.IsSuccessStatusCode)
        {
            // Log IdP OAuth error fields for support; never forward them to the client
            // (error_description can contain internal IdP infrastructure details).
            string? idpError = null;
            string? idpDescription = null;
            try
            {
                using var doc = JsonDocument.Parse(tokenBody);
                idpDescription = doc.RootElement.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
                idpError = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
            }
            catch
            {
                // Non-JSON error body — HttpStatus only.
            }

            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "token_exchange_rejected",
                Phase = "callback",
                SessionId = sessionId,
                HttpStatus = (int)tokenRes.StatusCode,
                IdpError = idpError,
                IdpErrorDescription = idpDescription,
                IsStepUp = isStepUp
            });
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
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "token_parse_failed",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail("Failed to parse token response.");
        }

        if (string.IsNullOrEmpty(idTokenRaw))
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_id_token",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status400BadRequest
            });
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
            ValidAudiences = [clientId],
            ValidateLifetime = true,
            ClockSkew = IdTokenClockSkew,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(idTokenRaw, validationParams, out _);
        }
        catch (SecurityTokenExpiredException ex)
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "expired_token",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status400BadRequest,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail("Id token has expired.");
        }
        catch (Exception ex)
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "token_validation_failed",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status400BadRequest,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
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
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_identity_claim",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusCodes.Status400BadRequest
            });
            return OidcExchangeResult.Fail("Callback token must contain an email or sub claim.");
        }

        // --- Sign the callback token with deployment-specific issuer/audience ---
        // Prevents cross-environment token confusion if the signing key were shared.
        var portalOrigin = _oidcSettings.Value.PortalOrigin;
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

    /// <summary>
    /// Writes the unified off-boarding log line and, when <paramref name="ex"/> is set,
    /// a second error-level log entry with the full exception for Datadog.
    /// </summary>
    private void LogOffboardingFailure(OidcCallbackFailureLogEntry entry, Exception? ex = null)
    {
        _callbackFailureLogger.Log(entry);
        if (ex != null)
        {
            _logger.LogError(ex, "OIDC exchange off-boarding: {Reason}", entry.Reason);
        }
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

    private IOidcCoreSettings GetSettings(bool isStepUp) =>
        isStepUp ? _oidcStepUpSettings.Value : _oidcSettings.Value;
}
