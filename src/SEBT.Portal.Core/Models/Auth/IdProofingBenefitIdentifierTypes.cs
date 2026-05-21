using System.Diagnostics.CodeAnalysis;

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
    public static bool IsSnapOrTanfPortalSelection([NotNullWhen(true)] string? idType) =>
        idType != null && SnapOrTanfPortalTypes.Contains(idType);

    /// <summary>
    /// Stores the submitted SNAP/TANF identifier on the user so DC warehouse household lookups can fall back to IC + DOB
    /// after login when rows use IC as <c>PortalID</c> instead of portal email.
    /// </summary>
    public static void PersistBenefitIdentifierOnUser(User user, string? idType, string? idValue)
    {
        if (string.IsNullOrWhiteSpace(idValue) || !IsSnapOrTanfPortalSelection(idType))
        {
            return;
        }

        var trimmed = idValue.Trim();
        if (string.Equals(idType, "snapAccountId", StringComparison.Ordinal)
            || string.Equals(idType, "snapPersonId", StringComparison.Ordinal))
        {
            user.SnapId = trimmed;
            return;
        }

        user.TanfId = trimmed;
    }
}
