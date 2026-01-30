namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Utility class for normalizing email addresses to ensure consistent storage and comparison.
/// </summary>
public static class EmailNormalizer
{
    /// <summary>
    /// Normalizes an email address to lowercase and trims whitespace.
    /// </summary>
    /// <param name="email">The email address to normalize.</param>
    /// <returns>The normalized (lowercase, trimmed) email address.</returns>
    /// <exception cref="ArgumentException">Thrown when the email is null or whitespace.</exception>
    public static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        }

        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes an email address to lowercase and trims whitespace, or returns null if the input is null or whitespace.
    /// </summary>
    /// <param name="email">The email address to normalize, or null/whitespace.</param>
    /// <returns>The normalized (lowercase, trimmed) email address, or null if the input was null or whitespace.</returns>
    public static string? NormalizeOrNull(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }
}
