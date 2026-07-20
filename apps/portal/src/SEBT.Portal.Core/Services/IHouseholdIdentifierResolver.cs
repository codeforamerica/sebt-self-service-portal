using System.Security.Claims;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Resolves the household identifier to use for authorization and lookup based on the authenticated user and state configuration.
/// State configuration determines which ID type(s) are preferred (e.g. Email, SNAP ID); the resolver returns the first type it can satisfy from the user's claims.
/// </summary>
public interface IHouseholdIdentifierResolver
{
    /// <summary>
    /// Resolves the preferred household identifier for the given user based on current state configuration.
    /// </summary>
    /// <param name="user">The authenticated user (claims principal).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The first preferred household identifier that can be resolved from the user's claims, or <c>null</c> if none is available.</returns>
    Task<HouseholdIdentifier?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
