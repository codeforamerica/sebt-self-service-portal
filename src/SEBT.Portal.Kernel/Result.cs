using Microsoft.Extensions.Logging;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Kernel;

public abstract class Result(bool isSuccess)
{
    public bool IsSuccess { get; } = isSuccess;

    public abstract string Message { get; }

    public static Result Success()
        => new SuccessResult();
    
    public static Result Aggregate(IReadOnlyList<Result> results)
        => new AggregateResult(results);
    
    public static Result Unauthorized(string message)
        => new UnauthorizedResult(message);
    
    public static Result ValidationFailed(IReadOnlyCollection<ValidationError> errors)
        => new ValidationFailedResult(errors);

    public static Result ValidationFailed(string key, string message)
        => new ValidationFailedResult(new[] { new ValidationError(key, message) });

    public static Result PreconditionFailed(PreconditionFailedReason reason, string? message = null)
        => new PreconditionFailedResult(reason, message);
    
    public static Result DependencyFailed(DependencyFailedReason reason, string? message = null)
        => new DependencyFailedResult(reason, message);
}

public abstract class Result<T>(bool isSuccess) : Result(isSuccess)
{
    public static Result<T> Success(T value)
        => new SuccessResult<T>(value);

    public new static Result<T> Unauthorized(string message)
        => new UnauthorizedResult<T>(message);
    
    public new static Result<T> ValidationFailed(IReadOnlyCollection<ValidationError> errors)
        => new ValidationFailedResult<T>(errors);

    public static Result<T> ValidationFailed(string key, IEnumerable<ValidationError> errors)
        => new ValidationFailedResult<T>(errors.Select(e => e with { Key = key }).ToList());

    public new static Result<T> ValidationFailed(string key, string message)
        => new ValidationFailedResult<T>([new ValidationError(key, message)]);

    public new static Result<T> PreconditionFailed(PreconditionFailedReason reason, string? message = null)
        => new PreconditionFailedResult<T>(reason, message);
    
    public new static Result<T> DependencyFailed(DependencyFailedReason reason, string? message = null)
        => new DependencyFailedResult<T>(reason, message);

    public Result<TOther> Map<TOther>(Func<T, TOther> successMapper) =>
        this switch
        {
            SuccessResult<T> success => Result<TOther>.Success(successMapper(success.Value)),
            _ => MapFailure<TOther>()
        };

    public Result<TOther> MapFailure<TOther>() =>
        this switch
        {
            SuccessResult<T> => throw new InvalidOperationException("Cannot map failure on success result"),
            ValidationFailedResult<T> validationFailed => Result<TOther>.ValidationFailed(validationFailed.Errors),
            PreconditionFailedResult<T> preconditionFailed => Result<TOther>.PreconditionFailed(preconditionFailed.Reason),
            _ => throw new InvalidOperationException("Unknown result type")
        };

    public abstract Result Map();
    
    public void LogFailure(ILogger logger, LogLevel logLevel = LogLevel.Warning)
    {
        switch (this)
        {
            case SuccessResult<T>:
                throw new InvalidOperationException("Cannot log failure for success result");
            case ValidationFailedResult<T> validationFailed:
                logger.Log(logLevel, "Validation failed: {Errors}", validationFailed.Errors);
                break;
            case PreconditionFailedResult<T> preconditionFailed:
                logger.Log(logLevel, "Precondition failed: {Reason}", preconditionFailed.Reason);
                break;
            default:
                throw new InvalidOperationException("Unknown result type");
        };
    }
}
