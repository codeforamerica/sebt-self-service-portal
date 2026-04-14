using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Completes an OIDC login flow. The controller reads the session ID from the cookie
/// and passes it here with the request body fields. The handler and validator own all
/// remaining validation (state allowlist, session lookup, callback token verification)
/// and business logic (user creation, IAL reconciliation, JWT generation).
/// </summary>
public class CompleteOidcLoginCommand : ICommand<CompleteOidcLoginResult>
{
    /// <summary>State code from the login request body (e.g. "co", "dc").</summary>
    [Required(ErrorMessage = "State code is required.")]
    public string? StateCode { get; init; }

    /// <summary>Callback token JWT from the OIDC exchange, containing IdP claims.</summary>
    [Required(ErrorMessage = "Callback token is required.")]
    public string? CallbackToken { get; init; }

    /// <summary>
    /// Pre-auth session ID read from the oidc_session HttpOnly cookie by the controller.
    /// Null when the cookie is absent — the handler returns Unauthorized (403), not ValidationFailed (400).
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>Optional return URL for step-up flows (relative path only).</summary>
    public string? ReturnUrl { get; init; }
}
