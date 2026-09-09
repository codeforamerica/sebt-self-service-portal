using System;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    public class RequestOtpCommandValidator(IValidator<RequestOtpCommand> validator)
        : IValidator<RequestOtpCommand>
    {
        public Task<ValidationResult> Validate(RequestOtpCommand command, CancellationToken cancellationToken = default)
        => validator.Validate(command, cancellationToken);
    }
}
