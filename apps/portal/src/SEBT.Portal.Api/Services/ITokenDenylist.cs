namespace SEBT.Portal.Api.Services;

/// <summary>
/// Server-side revocation list for portal JWTs, keyed by the token's <c>jti</c> claim.
/// Logout records the surrendered token here; the bearer middleware rejects denylisted
/// tokens on every authenticated request. Entries live only until the token would have
/// expired anyway, so the list stays small.
/// </summary>
public interface ITokenDenylist
{
    /// <summary>
    /// Records a token's <c>jti</c> until the token's own expiry (plus clock skew) passes.
    /// Never throws: a failed write degrades revocation, not logout.
    /// </summary>
    Task DenyAsync(string jti, DateTimeOffset tokenExpiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a <c>jti</c> has been revoked. Fails open (returns false) when the
    /// backing store is unreachable so a cache outage never locks out the portal.
    /// </summary>
    Task<bool> IsDeniedAsync(string jti, CancellationToken cancellationToken = default);
}
