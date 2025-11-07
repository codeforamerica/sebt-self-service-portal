namespace SEBT.Portal.Kernel.Results;

public class ValidationFailedResult(IReadOnlyCollection<ValidationError> errors) : ValidationResult(false)
{
    public override string Message => "The operation failed due to validation errors.";

    public IReadOnlyCollection<ValidationError> Errors => errors;
}

public class ValidationFailedResult<T>(IReadOnlyCollection<ValidationError> errors) : ValidationResult<T>(false)
{
    public override string Message => "The operation failed due to validation errors.";

    public IReadOnlyCollection<ValidationError> Errors => errors;
    
    public override Result Map() => new ValidationFailedResult(Errors);
}
