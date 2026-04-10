using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using System.Runtime.CompilerServices;

namespace SEBT.Portal.UseCases.Auth
{

    /// <summary>
    /// Handles the validation of one-time passwords (OTP) for user authentication.
    /// </summary>
    /// <remarks>
    /// This handler validates the OTP provided by the user against the stored OTP in the repository.
    /// If validation succeeds, the OTP is deleted from the repository to prevent reuse, the user is retrieved
    /// or created, and a JWT token is generated with ID proofing status included in the claims.
    /// </remarks>
    /// <param name="otpRepository">Repository for OTP storage and retrieval operations.</param>
    /// <param name="userRepository">Repository for user data and ID proofing status.</param>
    /// <param name="jwtTokenService">Service for generating JWT tokens for authenticated users.</param>
    /// <param name="validator">Validator for the <see cref="ValidateOtpCommand"/>.</param>
    /// <param name="logger">Logger for tracking OTP validation attempts and results.</param>
    /// <param name="featureManager">The feature manager for checking feature flags.</param>
    /// <param name="hostEnvironment">The host environment for checking the current environment.</param>
    public class ValidateOtpCommandHandler(
        IOtpRepository otpRepository,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IValidator<ValidateOtpCommand> validator,
        ILogger<ValidateOtpCommandHandler> logger,
        IFeatureManager featureManager,
        IHostEnvironment hostEnvironment)
        : ICommandHandler<ValidateOtpCommand, string>
    {
        public async Task<Result<string>> Handle(ValidateOtpCommand command, CancellationToken cancellationToken = default)
        {
            // Bypass OTP validation for specific email in staging environment when all criteria are met
            // 1. Feature flag "bypass_otp" is enabled
            // 2. The application is running in the staging environment
            // 3. The email in the request matches "dast-scanner@sebtportal.com"
            // 4. The OTP code in the request matches "123456"
            var byPassOtpEnabled = await featureManager.IsEnabledAsync(OtpBypassSettings.FeatureFlagName)
                && hostEnvironment.IsStaging()
                && !string.IsNullOrEmpty(command.Email)
                && command.Email == OtpBypassSettings.Email
                && command.Otp == OtpBypassSettings.OtpCode;

            if (byPassOtpEnabled)
            {
                // Bypassing OTP validation, directly retrieve or create the user and generate a token
                logger.LogWarning("OTP bypass is enabled. Skipping OTP validation for email {Email}", OtpBypassSettings.Email);
            }
            else
            {
                // Run full OTP validation for all other cases, including when the bypass criteria are not met
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
            }

            try
            {
                var (user, isNewUser) = await userRepository.GetOrCreateUserAsync(command.Email, cancellationToken);

                // A user who completed OTP authentication is at least IAL1; don't downgrade if already higher
                if (user.IalLevel < Core.Models.Auth.UserIalLevel.IAL1)
                {
                    user.IalLevel = Core.Models.Auth.UserIalLevel.IAL1;
                    await userRepository.UpdateUserAsync(user, cancellationToken);
                }

                var token = jwtTokenService.GenerateToken(user);

                // Delete OTP after successful validation
                if (!byPassOtpEnabled)
                {
                    await otpRepository.DeleteOtpCodeByEmailAsync(command.Email);
                }

                if (isNewUser)
                {
                    logger.LogInformation(
                        "New user authenticated via OTP for email {Email} with IAL level {IalLevel} and co-loaded status {IsCoLoaded}",
                        command.Email,
                        user.IalLevel,
                        user.IsCoLoaded);
                }
                else
                {
                    logger.LogInformation(
                        "Returning user authenticated via OTP for email {Email} with IAL level {IalLevel} and co-loaded status {IsCoLoaded}",
                        command.Email,
                        user.IalLevel,
                        user.IsCoLoaded);
                }

                logger.LogInformation(
                    "OTP validated successfully and JWT token generated for email {Email} with co-loaded status {IsCoLoaded}",
                    command.Email,
                    user.IsCoLoaded);

                return Result<string>.Success(token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing OTP validation for email {Email}", command.Email);
                return Result<string>.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    "An error occurred while processing the authentication request.");
            }
        }
    }

}
