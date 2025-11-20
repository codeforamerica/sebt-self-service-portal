using SEBT.Portal.Core.Models.Results;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    public class RequestOtpCommandHandler(IValidator<RequestOtpCommand> validator) : ICommandHandler<RequestOtpCommand, RequestOtpCommandResult>
    {
        public async Task<Result<RequestOtpCommandResult>> Handle(RequestOtpCommand command, CancellationToken cancellationToken)
        {
            var validationResult = await validator.Validate(command, cancellationToken);

            if (validationResult is ValidationFailedResult validationFailedResult)
            {
                return Result<RequestOtpCommandResult>.ValidationFailed(validationFailedResult.Errors);
            }

            return Result<RequestOtpCommandResult>.PreconditionFailed(PreconditionFailedReason.NotFound, "Not implemented");
        }
    }
}
