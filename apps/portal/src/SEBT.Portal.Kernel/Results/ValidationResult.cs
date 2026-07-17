namespace SEBT.Portal.Kernel.Results;

public abstract class ValidationResult(bool isSuccess) : Result(isSuccess)
{
    public static ValidationResult Failed(ValidationError error) => new ValidationFailedResult([error]);

    public static ValidationResult Failed(string key, string message) => new ValidationFailedResult([new ValidationError(key, message)]);


    public static ValidationResult Failed(IReadOnlyCollection<ValidationError> errors) => new ValidationFailedResult(errors);

    public static ValidationResult Passed() => new ValidationPassedResult();
}

public abstract class ValidationResult<T>(bool isSuccess) : Result<T>(isSuccess)
{
    public static ValidationResult<T> Failed(IReadOnlyCollection<ValidationError> errors) => new ValidationFailedResult<T>(errors);

    public static ValidationResult<T> Passed() => new ValidationPassedResult<T>();
}
