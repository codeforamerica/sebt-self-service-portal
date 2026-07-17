using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Validates <see cref="RequestCardReplacementCommand"/> using data annotations.
/// </summary>
public class RequestCardReplacementCommandValidator(
    IValidator<RequestCardReplacementCommand> validator)
    : IValidator<RequestCardReplacementCommand>
{
    public Task<ValidationResult> Validate(
        RequestCardReplacementCommand command,
        CancellationToken cancellationToken = default)
        => validator.Validate(command, cancellationToken);
}
