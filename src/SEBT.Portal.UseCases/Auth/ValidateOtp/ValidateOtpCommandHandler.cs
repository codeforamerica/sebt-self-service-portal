using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    /// <summary>
    /// Handles the validation of one-time passwords (OTP) for user authentication.
    /// </summary>
    /// <remarks>
    /// This handler validates the OTP provided by the user against the stored OTP in the repository.
    /// If validation succeeds, the OTP is deleted from the repository to prevent reuse.
    /// </remarks>
    /// <param name="otpRepository">Repository for OTP storage and retrieval operations.</param>
    /// <param name="validator">Validator for the <see cref="ValidateOtpCommand"/>.</param>
    /// <param name="logger">Logger for tracking OTP validation attempts and results.</param>
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
            await otpRepository.DeleteOtpCodeByEmailAsync(command.Email);
            logger.LogInformation("OTP validated successfully for email {Email}", command.Email);
            return new SuccessResult();
        }
    }

}
