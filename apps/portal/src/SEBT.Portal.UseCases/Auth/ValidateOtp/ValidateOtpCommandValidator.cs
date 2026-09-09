using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    public class ValidateOtpCommandValidator(IValidator<ValidateOtpCommand> validator)
        : IValidator<ValidateOtpCommand>
    {
        public Task<ValidationResult> Validate(ValidateOtpCommand command, CancellationToken cancellationToken = default)
        => validator.Validate(command, cancellationToken);
    }
}
