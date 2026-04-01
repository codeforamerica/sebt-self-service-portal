using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// JWT and client routing hints returned from OTP validation and token refresh.
/// </summary>
/// <param name="Token">Signed portal JWT.</param>
/// <param name="RequiresIdProofing">When true, the client should send the user through Socure ID proofing before benefits.</param>
public sealed record PortalAuthTokenResult(string Token, bool RequiresIdProofing);

/// <summary>
/// Shared rule for when post-login responses should direct users to ID proofing.
/// </summary>
public static class IdProofingRedirectPolicy
{
    /// <summary>
    /// Socure must be enabled (id-proofing API operational). Co-loaded users are treated as already sourced
    /// from authoritative state data. Completed proofing skips the redirect.
    /// </summary>
    public static bool RequiresIdProofingForUser(User user, SocureSettings socureSettings) =>
        socureSettings.Enabled
        && !user.IsCoLoaded
        && user.IdProofingStatus != IdProofingStatus.Completed;
}
