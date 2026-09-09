namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Utility class for normalizing identifier values to ensure consistent storage and comparison.
/// Strips dashes and spaces to produce a canonical form.
/// </summary>
public static class IdentifierNormalizer
{
    /// <summary>
    /// Normalizes an identifier by trimming whitespace and removing dashes and spaces.
    /// </summary>
    /// <param name="value">The identifier to normalize.</param>
    /// <returns>The normalized identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when the identifier is null or whitespace.</exception>
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        return NormalizeCore(value);
    }

    /// <summary>
    /// Normalizes an identifier by trimming whitespace and removing dashes and spaces, or returns null if the input is null or whitespace.
    /// </summary>
    /// <param name="value">The identifier to normalize, or null/whitespace.</param>
    /// <returns>The normalized identifier, or null if the input was null or whitespace.</returns>
    public static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeCore(value);
    }

    private static string NormalizeCore(string value) =>
        value.Trim().Replace("-", "").Replace(" ", "");
}
