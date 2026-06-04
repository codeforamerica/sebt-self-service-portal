using System.Security.Cryptography;
using Medallion.Threading;
using Microsoft.Extensions.Caching.Hybrid;

namespace SEBT.Portal.Api.Services;

/// <inheritdoc cref="IPreAuthSessionStore"/>
public sealed class PreAuthSessionStore : IPreAuthSessionStore
{
    private readonly HybridCache _cache;
    private readonly ILogger<PreAuthSessionStore> _logger;

    /// <summary>Pre-auth sessions expire after 15 minutes (covers IdP redirect + user interaction).</summary>
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = SessionTtl,
        LocalCacheExpiration = SessionTtl
    };

    internal const string CacheKeyPrefix = "oidc:preauth:";

    private readonly IDistributedLockProvider _lockProvider;

    /// <inheritdoc cref="PreAuthSessionStore"/>
    public PreAuthSessionStore(HybridCache cache, IDistributedLockProvider lockProvider, ILogger<PreAuthSessionStore> logger)
    {
        _cache = cache;
        _lockProvider = lockProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PreAuthSession> CreateAsync(
        string stateCode,
        string state,
        string codeVerifier,
        string redirectUri,
        bool isStepUp,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var sessionId = GenerateSessionId();
        var session = new PreAuthSession
        {
            Id = sessionId,
            State = state,
            CodeVerifier = codeVerifier,
            StateCode = stateCode,
            RedirectUri = redirectUri,
            IsStepUp = isStepUp,
            ReturnUrl = returnUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            Phase = PreAuthSessionPhase.Created
        };

        await _cache.SetAsync(CacheKey(sessionId), session, CacheOptions, cancellationToken: cancellationToken);
        _logger.LogInformation(
            "Pre-auth session created: SessionId={SessionId}, StateCode={StateCode} (reason=session_created)",
            sessionId, SanitizeForLog(stateCode));
        return session;
    }

    /// <inheritdoc/>
    public async Task<PreAuthSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
            CacheKey(sessionId),
            _ => ValueTask.FromResult<PreAuthSession?>(null),
            CacheOptions,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryAdvanceToCallbackCompletedAsync(
        string sessionId,
        string callbackTokenHash,
        CancellationToken cancellationToken = default)
    {
        await using (await _lockProvider.AcquireLockAsync(LockKey(sessionId), cancellationToken: cancellationToken))
        {
            var session = await GetAsync(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("Pre-auth session not found for callback advance: SessionId={SessionId} (reason=missing_session)", sessionId);
                return false;
            }
            if (session.Phase != PreAuthSessionPhase.Created)
            {
                _logger.LogWarning(
                    "Pre-auth session in wrong phase for callback: SessionId={SessionId}, Phase={Phase} (reason=replay)",
                    sessionId, session.Phase);
                return false;
            }

            var advanced = session with { Phase = PreAuthSessionPhase.CallbackCompleted, CallbackTokenHash = callbackTokenHash };
            await _cache.SetAsync(CacheKey(sessionId), advanced, CacheOptions, cancellationToken: cancellationToken);
            _logger.LogInformation("Pre-auth session advanced to CallbackCompleted: SessionId={SessionId} (reason=callback_completed)", sessionId);
            return true;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryAdvanceToLoginCompletedAsync(
        string sessionId,
        string callbackTokenHash,
        CancellationToken cancellationToken = default)
    {
        await using (await _lockProvider.AcquireLockAsync(LockKey(sessionId), cancellationToken: cancellationToken))
        {
            var session = await GetAsync(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("Pre-auth session not found for login advance: SessionId={SessionId} (reason=missing_session)", sessionId);
                return false;
            }
            if (session.Phase != PreAuthSessionPhase.CallbackCompleted)
            {
                _logger.LogWarning(
                    "Pre-auth session in wrong phase for login: SessionId={SessionId}, Phase={Phase} (reason=replay)",
                    sessionId, session.Phase);
                return false;
            }
            if (session.CallbackTokenHash != callbackTokenHash)
            {
                _logger.LogWarning(
                    "Pre-auth session callback token mismatch: SessionId={SessionId} (reason=token_mismatch)",
                    sessionId);
                return false;
            }

            var completed = session with { Phase = PreAuthSessionPhase.LoginCompleted };
            await _cache.SetAsync(CacheKey(sessionId), completed, CacheOptions, cancellationToken: cancellationToken);
            _logger.LogInformation("Pre-auth session advanced to LoginCompleted: SessionId={SessionId} (reason=login_completed)", sessionId);
            return true;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKey(sessionId), cancellationToken);
    }

    private static string CacheKey(string sessionId) => $"{CacheKeyPrefix}{sessionId}";
    private static string LockKey(string sessionId) => $"{CacheKeyPrefix}lock:{sessionId}";

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\u2028", string.Empty, StringComparison.Ordinal)
            .Replace("\u2029", string.Empty, StringComparison.Ordinal);
    }

    private static string GenerateSessionId()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
