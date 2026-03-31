namespace SEBT.Portal.Api.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request body for completing OIDC login after the Next.js server has exchanged the code and validated the id_token.
/// CallbackToken is a short-lived JWT signed by the Next.js app (OIDC_COMPLETE_LOGIN_SIGNING_KEY) containing the IdP claims.
/// When IsStepUp is true, updates the user's IAL level and ID proofing status instead of creating a new session.
/// When set, <c>returnUrl</c> must be a safe relative path (starts with <c>/</c>, not <c>//</c>, no scheme). Otherwise it is ignored.
/// </summary>
public record CompleteLoginRequest(
    [property: JsonPropertyName("stateCode")] string? StateCode,
    [property: JsonPropertyName("callbackToken")] string? CallbackToken,
    [property: JsonPropertyName("isStepUp")] bool IsStepUp = false,
    [property: JsonPropertyName("returnUrl")] string? ReturnUrl = null
);
