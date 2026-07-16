using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// DC <c>GetHouseholdByGuardian</c> only returns email-linked rows when <c>@isIdentityProofed = 1</c>
/// (plugin maps that to <see cref="UserIalLevel.IAL1plus"/>). Users mid ID-proofing are often
/// <see cref="UserIalLevel.IAL1"/> (OTP); claims can transiently lag as <see cref="UserIalLevel.None"/>.
/// For server-side Socure orchestration the portal loads the household keyed to the authenticated
/// login email and does not return that payload to the client, so when the deployment
/// requires a qualifying household we always use <see cref="UserIalLevel.IAL1plus"/> for that
/// warehouse read.
/// </summary>
public static class PreSocureHouseholdWarehouseIal
{
    /// <summary>
    /// Effective IAL to pass into state household reads keyed on login email while Socure orchestration runs.
    /// </summary>
    public static UserIalLevel ForEmailLinkedHouseholdRead(
        UserIalLevel userIal,
        bool requireQualifyingHouseholdForSocure)
    {
        if (requireQualifyingHouseholdForSocure)
        {
            return UserIalLevel.IAL1plus;
        }

        return userIal;
    }
}
