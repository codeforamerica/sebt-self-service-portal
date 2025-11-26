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
                logger.LogWarning("OTP validation failed for email {Email}: {Errors}",
                    command.Email,
                    string.Join(", ", validationFailedResult.Errors.Select(e => $"{e.Key}: {e.Message}")));
                return Result.ValidationFailed(validationFailedResult.Errors);
            }

            var otp = await otpRepository.GetOtpCodeByEmailAsync(command.Email);

            if (otp is null || otp.IsCodeValid(command.Otp) == false)
            {
                logger.LogWarning("Invalid or expired OTP attempt for email {Email}", command.Email);
                return Result.ValidationFailed(new[]
                {
                    new ValidationError("Otp", "The provided OTP is invalid or has expired.")
                });
            }

            logger.LogInformation("OTP validated successfully for email {Email}", command.Email);
            return new SuccessResult();
        }
    }

}