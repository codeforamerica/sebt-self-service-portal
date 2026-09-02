using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth.OidcCallback;

/// <summary>
/// Completes the OIDC authorization-code callback: validates the pre-auth session,
/// exchanges the code for a verified callback token, and advances the session.
/// </summary>
public class OidcCallbackCommand : ICommand<OidcCallbackResponse>
{
    /// <summary>Authorization code returned by the IdP redirect.</summary>
    public required string Code { get; init; }

    /// <summary>OAuth <c>state</c> parameter returned by the IdP redirect.</summary>
    public string? State { get; init; }

    /// <summary>
    /// Pre-auth session id from the <c>oidc_session</c> cookie. Null when the
    /// browser presented no cookie.
    /// </summary>
    public string? SessionId { get; init; }
}

/// <summary>Successful callback outcome.</summary>
/// <param name="CallbackToken">Short-lived signed token carrying the IdP claims.</param>
public sealed record OidcCallbackResponse(string CallbackToken);
