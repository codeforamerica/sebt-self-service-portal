using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    public class ValidateOtpCommandHandler(
        IOtpRepository otpRepository,
        IValidator<ValidateOtpCommand> validator,
        ILogger<ValidateOtpCommandHandler> logger)
        : ICommandHandler<ValidateOtpCommand>
    {
        public async Task<Result> Handle(ValidateOtpCommand command, CancellationToken cancellationToken = default)
        {
            var validationResult = await validator.Validate(command, cancellationToken);

            if (validationResult is ValidationFailedResult validationFailedResult)
            {
                return Result.ValidationFailed(validationFailedResult.Errors);
            }

            var otp = await otpRepository.GetOtpCodeByEmailAsync(command.Email);

            if (otp is null || otp.IsCodeValid(command.Otp) == false)
            {
                return Result.ValidationFailed(new[]
                {
                    new ValidationError("Otp", "The provided OTP is invalid or has expired.")
                });
            }

            return new SuccessResult();
        }
    }

}