namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response body for POST /api/auth/oidc/complete-login.
/// The portal JWT is returned via an HttpOnly cookie (see <c>AuthCookies</c>) — never in the response body.
/// </summary>
/// <param name="ReturnUrl">
/// For OIDC step-up flows, the relative path the user should be redirected to after verification succeeds.
/// Null for normal login — caller should redirect to the dashboard.
/// </param>
public record CompleteLoginResponse(string? ReturnUrl = null);
