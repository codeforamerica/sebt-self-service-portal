using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Validates <see cref="CompleteOidcLoginCommand"/> inputs: DataAnnotations (required fields)
/// then state code allowlist check.
/// </summary>
public class CompleteOidcLoginCommandValidator(
    IValidator<CompleteOidcLoginCommand> dataAnnotationsValidator,
    IStateAllowlist stateAllowlist)
    : IValidator<CompleteOidcLoginCommand>
{
    public async Task<ValidationResult> Validate(
        CompleteOidcLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await dataAnnotationsValidator.Validate(command, cancellationToken);
        if (result is ValidationFailedResult)
        {
            return result;
        }

        var resolvedStateCode = stateAllowlist.TryResolve(command.StateCode!);
        if (resolvedStateCode == null)
        {
            return ValidationResult.Failed("StateCode", "Unknown or unsupported state code.");
        }

        return ValidationResult.Passed();
    }
}
