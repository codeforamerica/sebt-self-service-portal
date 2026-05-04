using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
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
/// Validates the command, enforces the absolute-session-lifetime cap by reading the
/// inbound <c>auth_time</c> claim, and (when within the cap) generates a new JWT with
/// updated user claims.
/// </remarks>
public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    ISessionRefreshTokenService jwtTokenService,
    IValidator<RefreshTokenCommand> validator,
    IOptions<JwtSettings> jwtSettings,
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

        // The user ID lives in the principal's sub claim. Missing or malformed means
        // the caller isn't properly authenticated — reject rather than continuing.
        var userId = command.CurrentPrincipal.GetUserId();
        if (userId == null)
        {
            logger.LogWarning("Token refresh rejected: principal missing or invalid sub claim");
            return Result<string>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found.");
        }

        // Absolute-session-lifetime check (per OWASP / NIST SP 800-63B §7.1).
        // The JWT 'auth_time' claim records when the user originally authenticated; we never
        // re-stamp it on refresh, so this enforces a hard ceiling regardless of activity.
        var absoluteCapResult = CheckAbsoluteCap(command, userId.Value);
        if (absoluteCapResult is not null)
        {
            return absoluteCapResult;
        }

        try
        {
            // Look up by our internal user ID — uniform across OIDC and OTP users.
            var user = await userRepository.GetUserByIdAsync(userId.Value, cancellationToken);

            if (user == null)
            {
                logger.LogWarning(
                    "Token refresh attempted for non-existent UserId {UserId}", userId);
                return Result<string>.PreconditionFailed(
                    PreconditionFailedReason.NotFound, "User not found.");
            }

            // Pass all existing JWT claims through — for OIDC users, this preserves
            // IAL and other IdP-derived claims. For OTP users, GenerateForSessionRefresh will
            // prefer user object values (from DB) over these claims.
            var token = jwtTokenService.GenerateForSessionRefresh(user, command.CurrentPrincipal);

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
            logger.LogError(ex, "Error refreshing token for UserId {UserId}", userId);
            return Result<string>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "An error occurred while refreshing the authentication token.");
        }
    }

    /// <summary>
    /// Returns an Unauthorized result when the inbound principal's auth_time is missing,
    /// unparseable, or older than the configured absolute cap. Returns null when refresh
    /// may proceed.
    /// </summary>
    private Result<string>? CheckAbsoluteCap(RefreshTokenCommand command, Guid userId)
    {
        // Standard OIDC/JWT claim name (RFC 7519/OIDC Core); using the string literal keeps
        // the UseCases layer free of a JWT-package dependency.
        const string AuthTimeClaim = "auth_time";
        var authTimeClaim = command.CurrentPrincipal.FindFirst(AuthTimeClaim)?.Value;
        if (!long.TryParse(authTimeClaim, out var authTimeUnixSeconds))
        {
            logger.LogWarning(
                "Token refresh rejected: missing or invalid auth_time claim for UserId {UserId}", userId);
            return Result<string>.Unauthorized("Session is invalid; please sign in again.");
        }

        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ageSeconds = nowUnixSeconds - authTimeUnixSeconds;
        var capSeconds = jwtSettings.Value.AbsoluteExpirationMinutes * 60L;
        if (ageSeconds >= capSeconds)
        {
            logger.LogInformation(
                "Token refresh rejected: absolute session lifetime exceeded for UserId {UserId} " +
                "(age={AgeSeconds}s, cap={CapSeconds}s)", userId, ageSeconds, capSeconds);
            return Result<string>.Unauthorized("Session has reached its maximum lifetime; please sign in again.");
        }

        return null;
    }
}
