using System.Security.Claims;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth.CompleteOidcLogin;

/// <summary>
/// Orchestrates OIDC login completion. Verifies the callback token was issued for the
/// pre-auth session and has not been used before, resolves the portal user (existing-only
/// for step-up; get-or-create for login), and mints the portal JWT. The session is
/// consumed on the first advance — later failures intentionally leave it unusable.
/// </summary>
public class CompleteOidcLoginCommandHandler(
    IPreAuthSessionStore sessionStore,
    IOidcExchangeService exchangeService,
    IUserRepository userRepository,
    IOidcTokenService oidcTokenService,
    IOidcCallbackFailureLogger callbackFailureLogger,
    ILogger<CompleteOidcLoginCommandHandler> logger)
    : ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResponse>
{
    // HTTP statuses recorded on off-boarding log entries; pass-through diagnostic data
    // matching what the API layer returns for the corresponding result type.
    private const int StatusForbidden = 403;
    private const int StatusBadRequest = 400;
    private const int StatusServiceUnavailable = 503;

    public async Task<Result<CompleteOidcLoginResponse>> Handle(
        CompleteOidcLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        // --- Require the pre-auth session (bound to the browser via cookie) ---
        if (string.IsNullOrEmpty(command.SessionId))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "complete-login",
                HttpStatus = StatusForbidden
            });
            return Result<CompleteOidcLoginResponse>.Forbidden("Missing pre-auth session.");
        }

        // --- Retrieve session (stateCode, isStepUp, returnUrl are authoritative from here) ---
        var session = await sessionStore.GetAsync(command.SessionId, cancellationToken);
        if (session == null)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "complete-login",
                SessionId = command.SessionId,
                HttpStatus = StatusForbidden
            });
            return Result<CompleteOidcLoginResponse>.Forbidden(
                "Pre-auth session invalid, expired, or already used.");
        }

        // --- Verify the callback token matches this session and hasn't been consumed ---
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(command.CallbackToken);
        var advanced = await sessionStore.TryAdvanceToLoginCompletedAsync(
            command.SessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "complete-login",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusForbidden
            });
            return Result<CompleteOidcLoginResponse>.Forbidden(
                "Pre-auth session invalid, expired, or already used.");
        }

        // Remove the session from cache (defense-in-depth: even if the phase check were
        // bypassed, the code_verifier is gone from memory). The API layer clears the
        // pre-auth cookie whenever the session was consumed.
        await sessionStore.RemoveAsync(command.SessionId, cancellationToken);

        // --- Validate the callback token cryptographically (signature + issuer/audience) ---
        var tokenValidation = exchangeService.ValidateCallbackToken(command.CallbackToken);
        if (tokenValidation.NotConfigured)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "complete_login_not_configured",
                Phase = "complete-login",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusServiceUnavailable
            });
            return Result<CompleteOidcLoginResponse>.DependencyFailed(
                DependencyFailedReason.NotConfigured, "Complete-login not configured.");
        }

        if (!tokenValidation.Success)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "invalid_callback_token",
                Phase = "complete-login",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest,
                ApiError = tokenValidation.Error
            });
            logger.LogError(
                "OIDC complete-login off-boarding: invalid_callback_token (SessionId={SessionId}): {Error}",
                command.SessionId, tokenValidation.Error);
            return Result<CompleteOidcLoginResponse>.ValidationFailed(
                "callbackToken", "Invalid or expired callback token.");
        }

        var principal = tokenValidation.Principal!;

        // Extract sub + email from principal for user lookup. The token service handles
        // all claim processing (filtering, verification, IAL derivation).
        var subClaim = principal.FindFirst("sub")?.Value;
        var email = GetEmailFromClaims(principal);
        var phoneClaim = principal.FindFirst("phone")?.Value;
        var maskedPhone = PiiMasker.MaskPhone(phoneClaim);

        if (phoneClaim == null)
        {
            logger.LogError("OIDC incoming claims missing 'phone' (SessionId={SessionId})", command.SessionId);
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(subClaim))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_identity_claim",
                Phase = "complete-login",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest
            });
            return Result<CompleteOidcLoginResponse>.ValidationFailed(
                "callbackToken", "Callback token must contain an email or sub claim.");
        }

        if (string.IsNullOrWhiteSpace(subClaim))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_sub_claim",
                Phase = "complete-login",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest
            });
            return Result<CompleteOidcLoginResponse>.ValidationFailed(
                "callbackToken", "Callback token must contain a sub claim.");
        }

        User user;

        if (session.IsStepUp)
        {
            var existingEntity = await userRepository.GetUserByExternalIdAsync(subClaim, cancellationToken);
            if (existingEntity == null)
            {
                callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
                {
                    Reason = "step_up_user_not_found",
                    Phase = "complete-login",
                    SessionId = command.SessionId,
                    IsStepUp = true,
                    HttpStatus = StatusBadRequest
                });
                return Result<CompleteOidcLoginResponse>.PreconditionFailed(
                    PreconditionFailedReason.NotFound,
                    "Step-up requires an existing session. Please sign in again.");
            }

            user = existingEntity;
        }
        else
        {
            // Pass email from IdP claims as a migration hint: if no user exists for
            // this sub but one exists for this email, adopt that legacy record.
            // TODO: Remove email parameter once all existing users have been migrated.
            var emailHint = principal.FindFirst("email")?.Value;
            var (createdUser, _) = await userRepository.GetOrCreateUserByExternalIdAsync(
                subClaim, emailHint, cancellationToken);
            user = createdUser;
        }

        // The token service handles claim filtering, verification translation,
        // IAL derivation, and timestamp computation.
        var tokenResult = oidcTokenService.GenerateForOidcLogin(user, principal, session.IsStepUp);

        if (!tokenResult.IsSuccess)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "token_generation_failed",
                Phase = "complete-login",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest,
                ApiError = tokenResult.Message
            });
            return Result<CompleteOidcLoginResponse>.ValidationFailed(
                "stepUp", "Step-up verification failed. Please try again.");
        }

        logger.LogInformation(
            "OIDC {FlowType} complete: UserId {UserId}, Phone={MaskedPhone}, SessionId={SessionId}",
            session.IsStepUp ? "step-up" : "login", user.Id, maskedPhone, command.SessionId);

        var safeReturnUrl = session.IsStepUp ? session.ReturnUrl : null;
        return Result<CompleteOidcLoginResponse>.Success(
            new CompleteOidcLoginResponse(tokenResult.Value, safeReturnUrl));
    }

    /// <summary>
    /// Gets the email (or subject) from the callback token claims for portal user lookup.
    /// </summary>
    private static string? GetEmailFromClaims(ClaimsPrincipal principal)
    {
        var emailClaim = principal.FindFirst("email");
        if (!string.IsNullOrEmpty(emailClaim?.Value))
            return emailClaim.Value;
        var subClaim = principal.FindFirst("sub");
        return subClaim?.Value;
    }
}
