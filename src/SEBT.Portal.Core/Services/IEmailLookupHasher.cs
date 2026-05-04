namespace SEBT.Portal.Core.Services;

/// <summary>
/// Computes a deterministic 64-character hex MAC for normalized email lookups (equality in SQL via persisted email-hash column).
/// Separate from identifier hashing normalization rules used for SNAP/TANF/SSN.
/// </summary>
public interface IEmailLookupHasher
{
    /// <inheritdoc cref="NormalizeAndHash"/>
    string? NormalizeAndHash(string? email);

    /// <summary>Returns null when email is null/whitespace; otherwise trims + lowercases then MACs UTF-8.</summary>
    string? HashNormalized(string? normalizedLowercaseTrimmedEmail);
}
