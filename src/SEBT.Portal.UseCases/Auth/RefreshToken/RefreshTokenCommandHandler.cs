using System.Linq;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
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
/// <param name="validator">Validator for the <see cref="RefreshTokenCommand"/>.</param>
/// <param name="logger">Logger for tracking token refresh attempts and results.</param>
public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IValidator<RefreshTokenCommand> validator,
    ILogger<RefreshTokenCommandHandler> logger)
    : ICommandHandler<RefreshTokenCommand, string>
{
    public async Task<Result<string>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailedResult)
        {
            logger.LogWarning("Token refresh validation failed: {Errors}",
                string.Join(", ", validationFailedResult.Errors.Select(e => $"{e.Key}: {e.Message}")));
            return Result<string>.ValidationFailed(validationFailedResult.Errors);
        }

        try
        {
            User? user;
            if (!string.IsNullOrEmpty(command.ExternalProviderId))
            {
                // OIDC user — look up by IdP subject, not email
                user = await userRepository.GetUserByExternalIdAsync(
                    command.ExternalProviderId, cancellationToken);
            }
            else
            {
                // OTP user — look up by email (existing behavior)
                user = await userRepository.GetUserByEmailAsync(command.Email, cancellationToken);
            }

            if (user == null)
            {
                logger.LogWarning("Token refresh attempted for non-existent user");
                return Result<string>.PreconditionFailed(
                    PreconditionFailedReason.NotFound, "User not found.");
            }

            // Pass all existing JWT claims through — for OIDC users, this preserves
            // IAL and other IdP-derived claims. For OTP users, GenerateToken will
            // prefer user object values (from DB) over these claims.
            var additionalClaims = command.CurrentPrincipal.Claims
                .DistinctBy(c => c.Type)
                .ToDictionary(c => c.Type, c => c.Value);
            var token = jwtTokenService.GenerateToken(user, additionalClaims);

            var maskedPhone = PiiMasker.MaskPhone(
                command.CurrentPrincipal.FindFirst("phone")?.Value
                ?? command.CurrentPrincipal.FindFirst("phone_number")?.Value);
            logger.LogInformation(
                "Token refreshed successfully for UserId {UserId}, Phone={MaskedPhone}",
                user.Id, maskedPhone);

            return Result<string>.Success(token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing token");
            return Result<string>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "An error occurred while refreshing the authentication token.");
        }
    }
}

