using Microsoft.Extensions.Logging;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth
{
    public class RequestOtpCommandHandler(
        IValidator<RequestOtpCommand> validator,
        IOtpGenerator otpGenerator,
        IEmailSender emailService,
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

            try
            {
                var otp = new OtpCode(otpGenerator.GenerateOtp(), command.Email);
                await otpRepository.SaveOtpCodeAsync(otp);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while attemting to persist the OTP request for email {Email}", command.Email);
                return Result.DependencyFailed(DependencyFailedReason.Timeout,
                    $"An error occurred while processing the OTP request");
            }

            try
            {
                await emailService.SendEmailAsync(command.Email, "", "");
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while sending the OTP email for email {Email}", command.Email);
                return Result.DependencyFailed(DependencyFailedReason.Timeout,
                    $"An error occurred while processing the OTP request");
            }
            return Result.Success();
        }
    }
}
