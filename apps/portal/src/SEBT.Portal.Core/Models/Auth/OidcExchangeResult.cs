namespace SEBT.Portal.Core.Models.Auth;

/// <summary>Result of the OIDC code exchange.</summary>
public sealed record OidcExchangeResult
{
    /// <summary>True when exchange + verification succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Signed callback token (short-lived JWT containing IdP claims). Null on failure.</summary>
    public string? CallbackToken { get; init; }

    /// <summary>Phone claim value extracted during the exchange (for diagnostic logging). Null when absent or on failure.</summary>
    public string? PhoneClaim { get; init; }

    /// <summary>Human-readable error message for the client. Null on success.</summary>
    public string? Error { get; init; }

    /// <summary>HTTP status code to return to the client. 200 on success.</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>Creates a successful result with the given callback token.</summary>
    public static OidcExchangeResult Ok(string callbackToken, string? phoneClaim = null) => new()
    {
        Success = true,
        CallbackToken = callbackToken,
        PhoneClaim = phoneClaim,
        StatusCode = 200
    };

    /// <summary>Creates a failed result with the given error message and HTTP status code.</summary>
    public static OidcExchangeResult Fail(string error, int statusCode = 400) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode
    };
}
