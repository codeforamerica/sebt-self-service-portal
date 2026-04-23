namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// SNAP/TANF identifier options from the id-proofing onboarding form. These values are matched in-portal
/// against co-loaded records; they are not sent to Socure as <c>national_id</c>.
/// Co-loaded users who submit one of these types may be streamlined to completed proofing without a Socure match.
/// </summary>
public static class IdProofingBenefitIdentifierTypes
{
    private static readonly HashSet<string> SnapOrTanfPortalTypes = new(StringComparer.Ordinal)
    {
        "snapAccountId",
        "snapPersonId",
        "tanfAccountId",
        "tanfPersonId",
    };

    /// <summary>
    /// True when <paramref name="idType"/> is a SNAP or TANF selection from the portal onboarding UI
    /// (aligned with the web <c>IdType</c> enum and any future TANF variants).
    /// </summary>
    public static bool IsSnapOrTanfPortalSelection(string? idType) =>
        idType != null && SnapOrTanfPortalTypes.Contains(idType);
}
