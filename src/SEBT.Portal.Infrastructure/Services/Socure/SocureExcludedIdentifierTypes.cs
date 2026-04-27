using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Services.Socure;

/// <summary>
/// Benefit-program identifiers are not sent on Socure evaluation requests; see <see cref="IdProofingBenefitIdentifierTypes"/>.
/// Medicaid IDs are also excluded because they do not match Socure's <c>national_id</c> schema
/// (exactly 4 or 9 digits), so they stay as a state-plugin-only identifier.
/// </summary>
internal static class SocureExcludedIdentifierTypes
{
    /// <summary>
    /// When true, <c>national_id</c> must not be set for this portal id type.
    /// SNAP/TANF stay in-portal (matched against co-loaded records) and Medicaid is excluded
    /// because its 7 or 8 digit format is incompatible with Socure's national_id schema.
    /// Comparison is ordinal to match the existing <see cref="IdProofingBenefitIdentifierTypes"/> convention.
    /// </summary>
    public static bool IsExcludedFromSocurePayload(string? idType) =>
        IdProofingBenefitIdentifierTypes.IsSnapOrTanfPortalSelection(idType)
        || string.Equals(idType, "medicaidId", StringComparison.Ordinal);
}
