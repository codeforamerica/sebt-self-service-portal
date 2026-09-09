namespace SEBT.Portal.Api.Models;

using System.Text.Json.Serialization;

/// <summary>
/// request body for the server-side OIDC callback endpoint.
/// The <c>code_verifier</c> and <c>redirect_uri</c> are never accepted from the
/// client — they are read from the pre-auth session stored server-side. The browser
/// only sends the authorization code and metadata needed to match the session.
/// </summary>
public record OidcCallbackRequest(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("stateCode")] string? StateCode,
    [property: JsonPropertyName("isStepUp")] bool IsStepUp = false
);
