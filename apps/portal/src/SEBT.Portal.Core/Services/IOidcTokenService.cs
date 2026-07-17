using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Generates a portal JWT for OIDC-authenticated users. Accepts the validated
/// IdP callback token as a <see cref="ClaimsPrincipal"/>, handles verification
/// claim translation, IAL derivation, and ID proofing timestamp computation.
/// Returns <see cref="Result{T}"/> because step-up flows fail when the IdP
/// returns no verification claims.
/// </summary>
public interface IOidcTokenService
{
    Result<string> GenerateForOidcLogin(User user, ClaimsPrincipal idpPrincipal, bool isStepUp);
}
