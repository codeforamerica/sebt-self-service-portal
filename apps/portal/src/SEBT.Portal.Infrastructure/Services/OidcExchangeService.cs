using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// performs the OIDC token exchange and id_token verification entirely server-side.
/// The client secret, JWKS validation, and callback-token signing all happen here;
/// UseCases handlers orchestrate the flow through <see cref="IOidcExchangeService"/>.
///
/// The service is stateless between requests (all flow state lives in the pre-auth session
/// store). Inject as scoped or transient.
/// </summary>
public sealed class OidcExchangeService : IOidcExchangeService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OidcExchangeService> _logger;
    private readonly IOidcCallbackFailureLogger _callbackFailureLogger;

    /// <summary>strict exp check — ≤10 seconds clock skew tolerance.</summary>
    private static readonly TimeSpan IdTokenClockSkew = TimeSpan.FromSeconds(10);

    private const int CallbackTokenExpirySec = 300; // 5 minutes, matching the old Next.js value

    // HTTP statuses recorded on off-boarding log entries. The API layer maps
    // OidcExchangeFailureReason to the same statuses for the actual response —
    // keep the two in sync (see OidcController).
    private const int StatusServiceUnavailable = 503;
    private const int StatusBadGateway = 502;
    private const int StatusBadRequest = 400;

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
        ILogger<OidcExchangeService> logger,
        IOidcCallbackFailureLogger callbackFailureLogger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
        _callbackFailureLogger = callbackFailureLogger;
    }

    /// <inheritdoc/>
    public async Task<OidcDiscoveryInfo> GetDiscoveryInfoAsync(
        bool isStepUp,
        CancellationToken cancellationToken = default)
    {
        var oidcConfig = await GetDiscoveryConfigAsync(isStepUp, cancellationToken);
        return new OidcDiscoveryInfo
        {
            AuthorizationEndpoint = oidcConfig.AuthorizationEndpoint,
            EndSessionEndpoint = oidcConfig.EndSessionEndpoint
        };
    }

    /// <summary>
    /// Fetches the cached OIDC discovery document for the configured IdP. Returns the
    /// <see cref="OpenIdConnectConfiguration"/> containing endpoint URLs (authorization,
    /// token, userinfo), signing keys, and issuer metadata.
    /// </summary>
    /// <param name="isStepUp">True to use the step-up IdP configuration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
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
        var clientId = isStepUp ? _config["Oidc:StepUp:ClientId"] : _config["Oidc:ClientId"];
        var clientSecret = isStepUp ? _config["Oidc:StepUp:ClientSecret"] : _config["Oidc:ClientSecret"];
        var signingKey = _config["Oidc:CompleteLoginSigningKey"];

        if (string.IsNullOrEmpty(clientId)
            || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(signingKey))
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "oidc_not_configured",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusServiceUnavailable
            });
            return OidcExchangeResult.Fail(OidcExchangeFailureReason.NotConfigured, "OIDC not configured.");
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
                HttpStatus = StatusBadGateway,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail(
                OidcExchangeFailureReason.DiscoveryUnavailable, "Failed to load OIDC discovery document.");
        }

        if (string.IsNullOrEmpty(oidcConfig.TokenEndpoint))
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "discovery_invalid",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusBadGateway
            });
            return OidcExchangeResult.Fail(
                OidcExchangeFailureReason.DiscoveryInvalid, "Invalid discovery document (missing token_endpoint).");
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
            return OidcExchangeResult.Fail(OidcExchangeFailureReason.ExchangeFailed, "Token exchange failed.");
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
            return OidcExchangeResult.Fail(
                OidcExchangeFailureReason.ExchangeFailed, "Token exchange was rejected by the identity provider.");
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
            return OidcExchangeResult.Fail(OidcExchangeFailureReason.ExchangeFailed, "Failed to parse token response.");
        }

        if (string.IsNullOrEmpty(idTokenRaw))
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_id_token",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusBadRequest
            });
            return OidcExchangeResult.Fail(OidcExchangeFailureReason.ExchangeFailed, "No id_token in token response.");
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
        catch (SecurityTokenExpiredException ex)
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "expired_token",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusBadRequest,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail(OidcExchangeFailureReason.ExchangeFailed, "Id token has expired.");
        }
        catch (Exception ex)
        {
            LogOffboardingFailure(new OidcCallbackFailureLogEntry
            {
                Reason = "token_validation_failed",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = isStepUp,
                HttpStatus = StatusBadRequest,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            }, ex);
            return OidcExchangeResult.Fail(OidcExchangeFailureReason.ExchangeFailed, "Id token validation failed.");
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
            if (!OidcClaims.InfrastructureClaimNames.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
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
                HttpStatus = StatusBadRequest
            });
            return OidcExchangeResult.Fail(
                OidcExchangeFailureReason.ExchangeFailed, "Callback token must contain an email or sub claim.");
        }

        // --- Sign the callback token with deployment-specific issuer/audience ---
        // Prevents cross-environment token confusion if the signing key were shared.
        var portalOrigin = CallbackTokenIssuer();
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

    /// <inheritdoc/>
    public OidcCallbackTokenResult ValidateCallbackToken(string callbackToken)
    {
        var signingKey = _config["Oidc:CompleteLoginSigningKey"];
        if (string.IsNullOrEmpty(signingKey))
        {
            return new OidcCallbackTokenResult { NotConfigured = true };
        }

        var portalOrigin = CallbackTokenIssuer();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = portalOrigin,
            ValidateAudience = true,
            ValidAudience = portalOrigin,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Use resolver instead of IssuerSigningKey to bypass kid-matching;
            // the callback token is signed without a kid header, which causes IDX10517
            // when JwtSecurityTokenHandler tries to match by kid.
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => [key]
        };
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false // Preserve original JWT claim names (sub, email)
        };

        try
        {
            var principal = handler.ValidateToken(callbackToken, validationParams, out _);
            return new OidcCallbackTokenResult { Principal = principal };
        }
        catch (Exception ex)
        {
            return new OidcCallbackTokenResult { Error = OidcLogSanitizer.Sanitize(ex.Message) };
        }
    }

    /// <summary>
    /// Deployment-specific issuer/audience for the callback token, derived from the portal's
    /// public origin so tokens can't be replayed across environments sharing a signing key.
    /// </summary>
    private string CallbackTokenIssuer() =>
        _config["Oidc:CallbackRedirectUri"]?.TrimEnd('/') ?? "sebt-portal";

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
}
