using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Generates a refreshed portal JWT by copying claims from the current session's
/// <see cref="ClaimsPrincipal"/>. The user entity is used only for the internal
/// user ID (JWT sub claim).
/// </summary>
public interface ISessionRefreshTokenService
{
    string GenerateForSessionRefresh(User user, ClaimsPrincipal currentPrincipal);
}
