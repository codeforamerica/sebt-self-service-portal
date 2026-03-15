namespace SEBT.Portal.Core.Exceptions;

/// <summary>
/// Thrown when a data update fails because another writer modified the record concurrently.
/// Infrastructure repositories translate technology-specific concurrency exceptions
/// (e.g., EF Core's DbUpdateConcurrencyException) into this domain exception so that
/// use case handlers can react without depending on infrastructure types.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException) { }
}
