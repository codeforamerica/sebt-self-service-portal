using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Handles the business logic of OIDC login completion: validates the callback token,
/// creates or updates the portal user, reconciles IAL from OIDC verification claims,
/// and returns a signed portal JWT.
/// </summary>
/// <remarks>
/// Session management (cookie, session store, state allowlist, phase advancement)
/// is handled by the controller before this handler runs.
/// </remarks>
public class CompleteOidcLoginCommandHandler(
    IValidator<CompleteOidcLoginCommand> validator,
    ICallbackTokenValidator callbackTokenValidator,
    IUserRepository userRepository,
    IJwtTokenService jwtService,
    IOptions<JwtSettings> jwtSettingsOptions,
    ILogger<CompleteOidcLoginCommandHandler> logger,
    IOidcVerificationClaimTranslator? verificationClaimTranslator = null)
    : ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResult>
{
    private const int MaxStepUpReturnUrlLength = 4096;

    public async Task<Result<CompleteOidcLoginResult>> Handle(
        CompleteOidcLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        // --- Input validation ---
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            return Result<CompleteOidcLoginResult>.ValidationFailed(validationFailed.Errors);
        }

        // --- Callback token validation + claim extraction ---
        var tokenResult = callbackTokenValidator.Validate(command.CallbackToken!);
        if (tokenResult == null)
        {
            return Result<CompleteOidcLoginResult>.ValidationFailed(
                [new ValidationError("CallbackToken", "Invalid or expired callback token.")]);
        }

        // --- Step-up vs normal login ---
        User user;
        if (command.IsStepUp)
        {
            var stepUpResult = await HandleStepUpLogin(tokenResult.Email, cancellationToken);
            if (!stepUpResult.IsSuccess)
            {
                return Result<CompleteOidcLoginResult>.ValidationFailed(
                    [new ValidationError("StepUp", stepUpResult.Message)]);
            }
            user = stepUpResult.Value;
        }
        else
        {
            user = await HandleNormalLogin(tokenResult.Email, tokenResult.AdditionalClaims, cancellationToken);
        }

        // --- Generate portal JWT ---
        var token = jwtService.GenerateToken(user, tokenResult.AdditionalClaims);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);

        string? safeReturnUrl = null;
        if (command.IsStepUp)
        {
            safeReturnUrl = TrySanitizeStepUpReturnUrl(command.ReturnUrl);
            if (safeReturnUrl == null && !string.IsNullOrWhiteSpace(command.ReturnUrl))
                logger.LogWarning("Step-up complete-login: returnUrl rejected (must be a safe relative path).");
        }

        return Result<CompleteOidcLoginResult>.Success(
            new CompleteOidcLoginResult(token, expiresAt, safeReturnUrl));
    }

    private async Task<Result<User>> HandleStepUpLogin(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var existingUser = await userRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser == null)
        {
            logger.LogWarning(
                "Step-up complete-login: no existing portal user for callback token; sign-in required first.");
            return Result<User>.ValidationFailed(
                [new ValidationError("StepUp", "Step-up requires an existing session. Please sign in again.")]);
        }

        existingUser.IalLevel = UserIalLevel.IAL1plus;
        existingUser.IdProofingStatus = IdProofingStatus.Completed;
        existingUser.IdProofingCompletedAt = DateTime.UtcNow;
        existingUser.UpdatedAt = DateTime.UtcNow;
        await userRepository.UpdateUserAsync(existingUser, cancellationToken);

        logger.LogInformation(
            "OIDC step-up complete-login succeeded: UserId {UserId}, IalLevel {IalLevel}, IdProofingStatus {IdProofingStatus}",
            existingUser.Id, existingUser.IalLevel, existingUser.IdProofingStatus);

        return Result<User>.Success(existingUser);
    }

    private async Task<User> HandleNormalLogin(
        string normalizedEmail,
        IReadOnlyDictionary<string, string> additionalClaims,
        CancellationToken cancellationToken)
    {
        var (user, _) = await userRepository.GetOrCreateUserAsync(normalizedEmail, cancellationToken);

        // A user who completed OIDC login is at least IAL1; don't downgrade if already higher
        if (user.IalLevel < UserIalLevel.IAL1)
        {
            user.IalLevel = UserIalLevel.IAL1;
            await userRepository.UpdateUserAsync(user, cancellationToken);
        }

        // Reconcile IAL from OIDC verification claims (e.g. CO's PingOne/Socure).
        // If the IdP says the user completed identity verification, update our DB
        // to match — the IdP is the source of truth for OIDC-based verification.
        if (verificationClaimTranslator != null)
        {
            var verification = verificationClaimTranslator.Translate(additionalClaims);
            if (verification != null)
            {
                ReconcileIalFromOidcVerification(user, verification);
                await userRepository.UpdateUserAsync(user, cancellationToken);

                logger.LogInformation(
                    "OIDC verification claim reconciled: UserId {UserId}, IalLevel {IalLevel}, IsExpired {IsExpired}, VerifiedAt {VerifiedAt}",
                    user.Id, user.IalLevel, verification.IsExpired, verification.VerifiedAt);
            }
        }

        return user;
    }

    /// <summary>
    /// Updates a user's IAL and proofing fields based on translated OIDC verification claims.
    /// If the verification is expired, resets to IAL1 (the user must re-verify).
    /// If valid, promotes to the verified IAL level.
    /// </summary>
    internal static void ReconcileIalFromOidcVerification(User user, OidcVerificationResult verification)
    {
        if (verification.IsExpired)
        {
            user.IalLevel = UserIalLevel.IAL1;
            user.IdProofingStatus = IdProofingStatus.Expired;
            return;
        }

        user.IalLevel = verification.IalLevel;
        user.IdProofingStatus = IdProofingStatus.Completed;
        if (verification.VerifiedAt.HasValue)
        {
            user.IdProofingCompletedAt = verification.VerifiedAt.Value;
        }
    }

    /// <summary>
    /// Step-up post-login navigation: only same-document relative paths (e.g. <c>/profile/address</c>).
    /// Rejects absolute URLs and scheme-relative paths so the API never echoes an open redirect.
    /// </summary>
    internal static string? TrySanitizeStepUpReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return null;
        var t = returnUrl.Trim();
        if (t.Length > MaxStepUpReturnUrlLength)
            return null;
        if (!t.StartsWith("/", StringComparison.Ordinal))
            return null;
        if (t.StartsWith("//", StringComparison.Ordinal))
            return null;
        var pathPart = t;
        var qIdx = t.IndexOf('?', StringComparison.Ordinal);
        if (qIdx >= 0)
            pathPart = t[..qIdx];
        if (pathPart.Contains("://", StringComparison.Ordinal))
            return null;
        if (t.Contains("\\", StringComparison.Ordinal))
            return null;
        if (t.Contains("\r", StringComparison.Ordinal) || t.Contains("\n", StringComparison.Ordinal)
            || t.Contains("\0", StringComparison.Ordinal))
            return null;
        return t;
    }
}
