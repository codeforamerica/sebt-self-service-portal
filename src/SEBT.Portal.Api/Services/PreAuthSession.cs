namespace SEBT.Portal.Api.Services;

/// <summary>
/// server-side pre-auth session created at <c>GET /api/auth/oidc/{stateCode}/config</c>.
/// Binds the OIDC PKCE flow to the browser that initiated it via the <c>oidc_session</c> cookie.
/// State transitions: <c>Created → CallbackCompleted → LoginCompleted</c>.
/// Any attempt to advance from a state other than the immediately preceding one fails.
///
/// Immutable: state transitions produce a new instance via <c>with</c> expressions so concurrent
/// reads on a shared cache reference cannot observe partial mutations.
/// </summary>
public sealed record PreAuthSession
{
    /// <summary>Cryptographically random session ID (also the cookie value).</summary>
    public required string Id { get; init; }

    /// <summary>CSRF / OIDC state parameter sent to the IdP.</summary>
    public required string State { get; init; }

    /// <summary>PKCE code_verifier — never exposed to the browser.</summary>
    public required string CodeVerifier { get; init; }

    /// <summary>Tenant identifier (e.g. "co").</summary>
    public required string StateCode { get; init; }

    /// <summary>True when this is a step-up (IAL1+) flow.</summary>
    public bool IsStepUp { get; init; }

    /// <summary>redirect_uri that was sent in the authorization request.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>When the session was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Current lifecycle phase.</summary>
    public PreAuthSessionPhase Phase { get; init; } = PreAuthSessionPhase.Created;

    /// <summary>
    /// SHA-256 hash of the callback token issued at the <c>Callback</c> step.
    /// Stored so <c>CompleteLogin</c> can verify the presented token matches
    /// without storing the token itself.
    /// </summary>
    public string? CallbackTokenHash { get; init; }
}

/// <summary>Lifecycle phases of a pre-auth session.</summary>
public enum PreAuthSessionPhase
{
    /// <summary>Session created by <c>GetConfig</c>; awaiting callback.</summary>
    Created = 0,

    /// <summary>Callback succeeded; callback token was issued. Awaiting <c>CompleteLogin</c>.</summary>
    CallbackCompleted = 1,

    /// <summary>Portal JWT was issued; session is consumed and cannot be reused.</summary>
    LoginCompleted = 2
}
