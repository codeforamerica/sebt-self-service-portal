using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Generates a portal JWT for OTP-authenticated (local login) users.
/// All claims are sourced from the <see cref="User"/> entity.
/// </summary>
public interface ILocalLoginTokenService
{
    string GenerateForLocalLogin(User user);
}
