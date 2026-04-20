using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Command for refreshing a JWT token with updated user information.
/// </summary>
public class RefreshTokenCommand : ICommand<string>
{
    /// <summary>
    /// The portal's internal user ID, extracted from the authenticated JWT's sub claim.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "User ID must be a positive integer.")]
    public required int UserId { get; init; }

    /// <summary>
    /// The current ClaimsPrincipal for the request. IMPORTANT: Used to preserve existing claims
    /// (e.g. IAL from IdP for OIDC users) when generating the refreshed token.
    /// </summary>
    [Required(ErrorMessage = "CurrentPrincipal is required.")]
    public required ClaimsPrincipal CurrentPrincipal { get; init; }
}
