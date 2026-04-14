namespace SEBT.Portal.Kernel.Results;

public class ForbiddenResult(string message) : Result(false)
{
    public override string Message => message;
}

public class ForbiddenResult<T>(string message, IDictionary<string, object?>? extensions = null) : Result<T>(false)
{
    /// <summary>
    /// Additional structured data describing why access was denied.
    /// The API layer determines how to serialize this (e.g., as ProblemDetails extensions).
    /// </summary>
    public IDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();

    public override string Message => message;

    public override Result Map() => new ForbiddenResult(Message);
}
