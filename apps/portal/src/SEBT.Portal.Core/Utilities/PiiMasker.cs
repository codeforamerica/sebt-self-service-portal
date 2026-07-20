using System.Text.RegularExpressions;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Utility class for partially masking PII fields (email, phone, address)
/// so users can recognize their data without exposing full details.
/// </summary>
public static partial class PiiMasker
{
    /// <summary>
    /// Masks an email address, preserving the first character of the local part and the full domain.
    /// Example: "jane@example.com" → "j***@example.com"
    /// </summary>
    /// <param name="email">The email address to mask.</param>
    /// <returns>The masked email, or null if the input is null or whitespace.</returns>
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex < 1)
        {
            // Malformed email — mask the entire value rather than exposing it
            return new string('*', email.Length);
        }

        var firstChar = email[0];
        var domain = email[atIndex..];
        return $"{firstChar}***{domain}";
    }

    /// <summary>
    /// Masks a phone number, preserving only the last 4 digits.
    /// Example: "(303) 555-0100" → "***-***-0100"
    /// </summary>
    /// <param name="phone">The phone number to mask.</param>
    /// <returns>The masked phone, or null if the input is null or whitespace.</returns>
    public static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        // Extract only the digits
        var digits = NonDigits().Replace(phone, "");

        if (digits.Length < 4)
        {
            // Too few digits to show last 4 — mask everything
            return "***";
        }

        var last4 = digits[^4..];

        return digits.Length switch
        {
            >= 10 => $"***-***-{last4}",  // Full 10+ digit number
            >= 7 => $"***-{last4}",         // 7-digit local number
            _ => $"***-{last4}"             // 4-6 digits
        };
    }

    /// <summary>
    /// Masks street address lines by returning a generic masked indicator.
    /// Callers should handle city, state, and ZIP code values separately if needed.
    /// </summary>
    /// <param name="streetAddress1">The primary street address line.</param>
    /// <param name="streetAddress2">The secondary street address line (apt, suite, etc.).</param>
    /// <returns>A masked street indicator, or null if both street address lines are null or whitespace.
    /// Callers should log a warning when this returns null — a missing address likely indicates
    /// an upstream data issue that warrants investigation.</returns>
    public static string? MaskStreetAddress(string? streetAddress1, string? streetAddress2)
    {
        if (string.IsNullOrWhiteSpace(streetAddress1) && string.IsNullOrWhiteSpace(streetAddress2))
        {
            return null;
        }

        return "****";
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}
