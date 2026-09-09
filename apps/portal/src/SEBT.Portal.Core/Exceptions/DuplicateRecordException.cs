namespace SEBT.Portal.Core.Exceptions;

/// <summary>
/// Thrown when a data insert fails because a record with the same unique constraint
/// already exists. Infrastructure repositories translate technology-specific exceptions
/// (e.g., EF Core's DbUpdateException with a unique index violation) into this domain
/// exception so that use case handlers can react without depending on infrastructure types.
/// </summary>
public class DuplicateRecordException : Exception
{
    public DuplicateRecordException(string message) : base(message) { }

    public DuplicateRecordException(string message, Exception innerException)
        : base(message, innerException) { }
}
