namespace SEBT.Portal.Api.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request body for completing OIDC login after the Next.js server has exchanged the code and validated the id_token.
/// CallbackToken is a short-lived JWT signed by the Next.js app (OIDC_COMPLETE_LOGIN_SIGNING_KEY) containing the IdP claims.
/// </summary>
public record CompleteLoginRequest(
    [property: JsonPropertyName("stateCode")] string? StateCode,
    [property: JsonPropertyName("callbackToken")] string? CallbackToken
);
