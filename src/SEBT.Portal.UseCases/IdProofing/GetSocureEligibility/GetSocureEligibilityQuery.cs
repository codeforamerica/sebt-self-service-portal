using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Checks whether Socure DocV remains available for ID proofing, given portal
/// household membership. Intended for DC when <see cref="IdProofingEligibilitySettings.RequireQualifyingHouseholdForSocure"/> is enabled.
/// </summary>
public class GetSocureEligibilityQuery : IQuery<SocureEligibilityResponse>
{
    /// <summary>
    /// The authenticated user's claims principal.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }
}
