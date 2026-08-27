namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Truncates and strips control characters from OIDC error strings before logging.
/// IdP <c>error_description</c> values may be long URL-encoded JSON blobs.
/// </summary>
public static class OidcLogSanitizer
{
    /// <summary>Max length for <c>error_description</c> and similar fields in logs.</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>Max length for short OAuth <c>error</c> codes (e.g. <c>invalid_grant</c>).</summary>
    public const int MaxErrorCodeLength = 128;

    /// <summary>
    /// Strips CR/LF and truncates for safe structured logging.
    /// </summary>
    public static string Sanitize(string? value, int maxLength = MaxDescriptionLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return sanitized[..maxLength] + "…";
    }
}
