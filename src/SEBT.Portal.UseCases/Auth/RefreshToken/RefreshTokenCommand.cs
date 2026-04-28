using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Command for refreshing a JWT token with updated user information. The user ID and
/// the claims to preserve are both read from <see cref="CurrentPrincipal"/>.
/// </summary>
public class RefreshTokenCommand : ICommand<string>
{
    /// <summary>
    /// The current ClaimsPrincipal for the request. The handler reads the portal user ID
    /// from the <c>sub</c> claim and preserves the remaining claims (e.g. IAL from IdP for
    /// OIDC users) when generating the refreshed token.
    /// </summary>
    [Required(ErrorMessage = "CurrentPrincipal is required.")]
    public required ClaimsPrincipal CurrentPrincipal { get; init; }
}
