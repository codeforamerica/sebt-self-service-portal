using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth
{
    public class ValidateOtpCommandHandler(
        IOtpRepository otpRepository,
        IValidator<ValidateOtpCommand> validator,
        ILogger<ValidateOtpCommandHandler> logger)
        : ICommandHandler<ValidateOtpCommand>
    {
        public Task<Result> Handle(ValidateOtpCommand command, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

}