using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Services.Socure;

/// <summary>
/// Benefit-program identifiers are not sent on Socure evaluation requests; see <see cref="IdProofingBenefitIdentifierTypes"/>.
/// </summary>
internal static class SocureExcludedIdentifierTypes
{
    /// <summary>
    /// When true, <c>national_id</c> must not be set for this portal id type (SNAP/TANF stay in-portal).
    /// </summary>
    public static bool IsExcludedFromSocurePayload(string? idType) =>
        IdProofingBenefitIdentifierTypes.IsSnapOrTanfPortalSelection(idType);
}
