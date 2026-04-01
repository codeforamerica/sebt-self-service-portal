using System.Linq;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Handles the refresh of JWT tokens for authenticated users.
/// </summary>
/// <remarks>
/// This handler validates the command, retrieves the current user information from the repository,
/// and generates a new JWT token with updated ID proofing status and other user claims.
/// </remarks>
/// <param name="userRepository">Repository for user data and ID proofing status.</param>
/// <param name="jwtTokenService">Service for generating JWT tokens.</param>
/// <param name="socureSettings">Socure configuration; when disabled, clients are not sent to ID proofing.</param>
/// <param name="validator">Validator for the <see cref="RefreshTokenCommand"/>.</param>
/// <param name="logger">Logger for tracking token refresh attempts and results.</param>
public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    SocureSettings socureSettings,
    IValidator<RefreshTokenCommand> validator,
    ILogger<RefreshTokenCommandHandler> logger)
    : ICommandHandler<RefreshTokenCommand, PortalAuthTokenResult>
{
    public async Task<Result<PortalAuthTokenResult>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);

        if (validationResult is ValidationFailedResult validationFailedResult)
        {
            logger.LogWarning("Token refresh validation failed for email {Email}: {Errors}",
                command.Email,
                string.Join(", ", validationFailedResult.Errors.Select(e => $"{e.Key}: {e.Message}")));
            return Result<PortalAuthTokenResult>.ValidationFailed(validationFailedResult.Errors);
        }

        try
        {
            var user = await userRepository.GetUserByEmailAsync(command.Email, cancellationToken);

            if (user == null)
            {
                logger.LogWarning("Token refresh attempted for non-existent user {Email}", command.Email);
                return Result<PortalAuthTokenResult>.PreconditionFailed(
                    PreconditionFailedReason.NotFound,
                    "User not found.");
            }

            var token = jwtTokenService.GenerateToken(user);
            var requiresIdProofing = IdProofingRedirectPolicy.RequiresIdProofingForUser(user, socureSettings);

            logger.LogInformation(
                "Token refreshed successfully for email {Email} with IAL level {IalLevel}, RequiresIdProofing {RequiresIdProofing}",
                command.Email,
                user.IalLevel,
                requiresIdProofing);

            return Result<PortalAuthTokenResult>.Success(new PortalAuthTokenResult(token, requiresIdProofing));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing token for email {Email}", command.Email);
            return Result<PortalAuthTokenResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "An error occurred while refreshing the authentication token.");
        }
    }
}
