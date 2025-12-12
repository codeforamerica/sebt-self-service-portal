using Microsoft.Extensions.Logging;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    /// <summary>
    /// Handles the request to generate and send a one-time password (OTP) to a user's email.
    /// </summary>
    /// <remarks>
    /// This handler validates the incoming command, generates a new OTP code, 
    /// persists it to the repository, and sends it to the user via email.
    /// </remarks>
    /// <param name="validator">The validator used to validate the <see cref="RequestOtpCommand"/>.</param>
    /// <param name="otpGenerator">The service used to generate OTP codes.</param>
    /// <param name="emailService">The service used to send the OTP to the user's email.</param>
    /// <param name="otpRepository">The repository used to persist OTP codes.</param>
    /// <param name="logger">The logger for recording errors and diagnostic information.</param>
    public class RequestOtpCommandHandler(
        IValidator<RequestOtpCommand> validator,
        IOtpGeneratorService otpGenerator,
        IOtpSenderService emailService,
        IOtpRepository otpRepository,
        ILogger<RequestOtpCommandHandler> logger)
        : ICommandHandler<RequestOtpCommand>
    {
        public async Task<Result> Handle(RequestOtpCommand command, CancellationToken cancellationToken)
        {
            var validationResult = await validator.Validate(command, cancellationToken);

            if (validationResult is ValidationFailedResult validationFailedResult)
            {
                return Result.ValidationFailed(validationFailedResult.Errors);
            }

            var otp = new OtpCode(otpGenerator.GenerateOtp(), command.Email);

            try
            {
                await otpRepository.SaveOtpCodeAsync(otp);
            }
            catch (TimeoutException e)
            {
                logger.LogError(e, "A timeout occurred while attempting to persist the OTP request for email {Email}", command.Email);
                return Result.DependencyFailed(DependencyFailedReason.Timeout,
                    $"A timeout occurred while processing the OTP request");
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while attempting to persist the OTP request for email {Email}", command.Email);
                return Result.DependencyFailed(DependencyFailedReason.ConnectionFailed,
                    $"An error occurred while processing the OTP request");
            }

            return await emailService.SendOtpAsync(command.Email, otp.Code);

        }
    }
}
