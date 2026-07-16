using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Kernel;

public interface IValidator<in T>
{
    Task<ValidationResult> Validate(T command, CancellationToken cancellationToken = default);
}
