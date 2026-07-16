namespace SEBT.Portal.Api.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Browser-reported OIDC callback failure (IdP redirect errors and missing OAuth params)
/// for server-side logging before redirect to off-boarding. Exchange and complete-login
/// failures are logged on the server only.
/// </summary>
public record OidcCallbackFailureReportRequest(
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("idpError")] string? IdpError = null,
    [property: JsonPropertyName("idpErrorDescription")] string? IdpErrorDescription = null,
    [property: JsonPropertyName("httpStatus")] int? HttpStatus = null,
    [property: JsonPropertyName("apiError")] string? ApiError = null,
    [property: JsonPropertyName("phase")] string? Phase = null,
    [property: JsonPropertyName("hasCode")] bool? HasCode = null,
    [property: JsonPropertyName("hasState")] bool? HasState = null);
