using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth.CompleteOidcLogin;

/// <summary>
/// Completes an OIDC login or step-up: verifies the callback token against the pre-auth
/// session, resolves the portal user, and mints the portal JWT.
/// </summary>
public class CompleteOidcLoginCommand : ICommand<CompleteOidcLoginResponse>
{
    /// <summary>Callback token issued at the callback step.</summary>
    public required string CallbackToken { get; init; }

    /// <summary>
    /// Pre-auth session id from the <c>oidc_session</c> cookie. Null when the
    /// browser presented no cookie.
    /// </summary>
    public string? SessionId { get; init; }
}

/// <summary>Successful completion outcome.</summary>
/// <param name="Token">The signed portal JWT (the API layer sets it as the session cookie).</param>
/// <param name="ReturnUrl">
/// For step-up flows, the safe relative path to return to after verification. Null for
/// normal logins.
/// </param>
public sealed record CompleteOidcLoginResponse(string Token, string? ReturnUrl);
