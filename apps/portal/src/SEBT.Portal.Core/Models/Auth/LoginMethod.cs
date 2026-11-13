namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// How a user authenticated into the portal
/// </summary>
public enum LoginMethod
{
    /// <summary>Email one-time password verified by the portal.</summary>
    Otp = 1,

    /// <summary>OpenID Connect sign-in through an external identity provider.</summary>
    Oidc = 2
}
