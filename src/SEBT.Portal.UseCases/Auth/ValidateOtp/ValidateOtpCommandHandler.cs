using System.Linq;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    /// <summary>
    /// Handles the validation of one-time passwords (OTP) for user authentication.
    /// </summary>
    /// <remarks>
    /// This handler validates the OTP provided by the user against the stored OTP in the repository.
    /// If validation succeeds, the OTP is deleted from the repository to prevent reuse and a JWT token is generated.
    /// </remarks>
    /// <param name="otpRepository">Repository for OTP storage and retrieval operations.</param>
    /// <param name="jwtTokenService">Service for generating JWT tokens for authenticated users.</param>
    /// <param name="validator">Validator for the <see cref="ValidateOtpCommand"/>.</param>
    /// <param name="logger">Logger for tracking OTP validation attempts and results.</param>
    public class ValidateOtpCommandHandler(
        IOtpRepository otpRepository,
        IJwtTokenService jwtTokenService,
        IValidator<ValidateOtpCommand> validator,
        ILogger<ValidateOtpCommandHandler> logger)
        : ICommandHandler<ValidateOtpCommand, string>
    {
        public async Task<Result<string>> Handle(ValidateOtpCommand command, CancellationToken cancellationToken = default)
        {
            var validationResult = await validator.Validate(command, cancellationToken);

            if (validationResult is ValidationFailedResult validationFailedResult)
            {
                logger.LogWarning("OTP validation failed for email {Email}: {Errors}",
                    command.Email,
                    string.Join(", ", validationFailedResult.Errors.Select(e => $"{e.Key}: {e.Message}")));
                return Result<string>.ValidationFailed(validationFailedResult.Errors);
            }

            var otp = await otpRepository.GetOtpCodeByEmailAsync(command.Email);

            if (otp is null || otp.IsCodeValid(command.Otp) == false)
            {
                logger.LogWarning("Invalid or expired OTP attempt for email {Email}", command.Email);
                return Result<string>.ValidationFailed(new[]
                {
                    new ValidationError("Otp", "The provided OTP is invalid or has expired.")
                });
            }

            try
            {
                var token = jwtTokenService.GenerateToken(command.Email);

                await otpRepository.DeleteOtpCodeByEmailAsync(command.Email);

                logger.LogInformation("OTP validated successfully and JWT token generated for email {Email}", command.Email);
                return Result<string>.Success(token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating JWT token for email {Email}", command.Email);
                return Result<string>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    "An error occurred while generating the authentication token.");
            }
        }
    }

}
