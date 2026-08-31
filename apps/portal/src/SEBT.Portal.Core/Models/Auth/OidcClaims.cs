namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Claim-name sets shared by every consumer of IdP-issued tokens.
/// </summary>
public static class OidcClaims
{
    /// <summary>
    /// Standard OIDC/JWT and IdP-infrastructure claim names excluded when copying IdP
    /// claims into the callback token or portal JWT. Single source of truth for the
    /// exchange service and the portal token service.
    /// </summary>
    public static readonly HashSet<string> InfrastructureClaimNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "iss", "aud", "iat", "exp", "nbf", "nonce", "at_hash", "c_hash",
        "auth_time", "acr", "amr", "azp", "sid", "jti",
        "env", "org", "p1.region"
    };
}
