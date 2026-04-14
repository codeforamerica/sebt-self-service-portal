using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Validates <see cref="CompleteOidcLoginCommand"/> inputs via DataAnnotations.
/// </summary>
public class CompleteOidcLoginCommandValidator(
    IValidator<CompleteOidcLoginCommand> dataAnnotationsValidator)
    : IValidator<CompleteOidcLoginCommand>
{
    public Task<ValidationResult> Validate(
        CompleteOidcLoginCommand command,
        CancellationToken cancellationToken = default)
        => dataAnnotationsValidator.Validate(command, cancellationToken);
}
