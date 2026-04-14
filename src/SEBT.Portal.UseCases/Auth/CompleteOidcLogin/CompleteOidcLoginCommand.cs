using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Completes an OIDC login after the controller has validated the pre-auth session.
/// Validates the callback token, creates or updates the portal user, reconciles IAL
/// from OIDC verification claims, and returns a signed portal JWT.
/// </summary>
/// <remarks>
/// Session management (cookie, session store, state allowlist, phase advancement) is
/// handled by the controller before this command runs. By the time the handler executes,
/// the session has been consumed and removed — the handler only needs the callback token
/// and the session's step-up flag.
/// </remarks>
public class CompleteOidcLoginCommand : ICommand<CompleteOidcLoginResult>
{
    /// <summary>Callback token JWT from the OIDC exchange, containing IdP claims.</summary>
    [Required(ErrorMessage = "Callback token is required.")]
    public string? CallbackToken { get; init; }

    /// <summary>Whether this login is a step-up (IAL elevation) flow. Determined by the pre-auth session.</summary>
    public bool IsStepUp { get; init; }

    /// <summary>Optional return URL for step-up flows (relative path only).</summary>
    public string? ReturnUrl { get; init; }
}
