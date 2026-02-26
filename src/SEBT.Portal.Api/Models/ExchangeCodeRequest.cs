namespace SEBT.Portal.Api.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request body for exchanging an authorization code for tokens on the backend (PKCE + client secret).
/// </summary>
public record ExchangeCodeRequest(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("code_verifier")] string? CodeVerifier
);
