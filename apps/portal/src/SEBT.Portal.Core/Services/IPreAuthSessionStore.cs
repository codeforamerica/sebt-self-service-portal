using System.Security.Cryptography;
using System.Text;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// manages the lifecycle of server-side pre-auth OIDC sessions.
/// Implementations store sessions in a cache (L1 memory + optional L2 Redis)
/// with an automatic TTL so abandoned flows expire without explicit cleanup.
/// </summary>
public interface IPreAuthSessionStore
{
    /// <summary>Creates a new pre-auth session and returns its ID (for the cookie).</summary>
    Task<PreAuthSession> CreateAsync(
        string stateCode,
        string state,
        string codeVerifier,
        string redirectUri,
        bool isStepUp,
        string? returnUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves a session by ID. Returns null if expired or not found.</summary>
    Task<PreAuthSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the session to <see cref="PreAuthSessionPhase.CallbackCompleted"/>
    /// and stores the callback token hash. Fails if the session is not in <c>Created</c> phase.
    /// </summary>
    /// <remarks>
    /// Implementations use a distributed lock to serialize concurrent transitions
    /// across all container instances (Redis-backed in production, SQL fallback otherwise).
    /// </remarks>
    Task<bool> TryAdvanceToCallbackCompletedAsync(
        string sessionId,
        string callbackTokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the session to <see cref="PreAuthSessionPhase.LoginCompleted"/>.
    /// Fails if the session is not in <c>CallbackCompleted</c> phase or the callback token
    /// hash doesn't match. Uses the same distributed lock as <see cref="TryAdvanceToCallbackCompletedAsync"/>.
    /// </summary>
    Task<bool> TryAdvanceToLoginCompletedAsync(
        string sessionId,
        string callbackTokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a session (used after login completion or on error cleanup).</summary>
    Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Computes the SHA-256 hash of a callback token for storage/comparison.</summary>
    static string HashCallbackToken(string callbackToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(callbackToken));
        return Convert.ToHexStringLower(bytes);
    }
}
