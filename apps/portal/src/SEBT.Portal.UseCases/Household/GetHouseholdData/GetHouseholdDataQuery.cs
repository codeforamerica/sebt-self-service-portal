using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Query to retrieve household data for an authenticated user.
/// The household identifier is resolved from the user's claims based on state configuration.
/// </summary>
public class GetHouseholdDataQuery : IQuery<Core.Models.Household.HouseholdData>
{
    /// <summary>
    /// The authenticated user's claims principal.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// When false, state connectors that support it (CO CBMS) skip the FIS card lookup.
    /// Honored only when <see cref="FeatureFlags.DeferEbtCardDataLoading"/> is enabled.
    /// </summary>
    public bool IncludeCardDetails { get; init; } = true;
}
