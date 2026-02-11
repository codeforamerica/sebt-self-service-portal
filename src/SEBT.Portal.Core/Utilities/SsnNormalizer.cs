namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Utility class for normalizing SSN values to ensure consistent storage and comparison.
/// Strips dashes and spaces to produce a canonical digits-only form.
/// </summary>
public static class SsnNormalizer
{
    /// <summary>
    /// Normalizes an SSN by trimming whitespace and removing dashes and spaces.
    /// </summary>
    /// <param name="ssn">The SSN to normalize.</param>
    /// <returns>The normalized SSN.</returns>
    /// <exception cref="ArgumentException">Thrown when the SSN is null or whitespace.</exception>
    public static string Normalize(string ssn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssn, nameof(ssn));

        return NormalizeCore(ssn);
    }

    /// <summary>
    /// Normalizes an SSN by trimming whitespace and removing dashes and spaces, or returns null if the input is null or whitespace.
    /// </summary>
    /// <param name="ssn">The SSN to normalize, or null/whitespace.</param>
    /// <returns>The normalized SSN, or null if the input was null or whitespace.</returns>
    public static string? NormalizeOrNull(string? ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn))
        {
            return null;
        }

        return NormalizeCore(ssn);
    }

    private static string NormalizeCore(string value) =>
        value.Trim().Replace("-", "").Replace(" ", "");
}
