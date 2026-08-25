using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Auth.OidcCallback;

/// <summary>
/// Orchestrates the server-side OIDC callback. Validates <c>state</c> against the stored
/// pre-auth session value, uses the stored <c>code_verifier</c> for the token exchange
/// (never from the browser), and advances the session to <c>CallbackCompleted</c>.
/// The <c>stateCode</c> and <c>isStepUp</c> values are read from the session — the
/// command only carries <c>code</c>, <c>state</c>, and the session id.
/// </summary>
public class OidcCallbackCommandHandler(
    IPreAuthSessionStore sessionStore,
    IOidcExchangeService exchangeService,
    IOidcCallbackFailureLogger callbackFailureLogger,
    ILogger<OidcCallbackCommandHandler> logger)
    : ICommandHandler<OidcCallbackCommand, OidcCallbackResponse>
{
    // HTTP statuses recorded on off-boarding log entries; pass-through diagnostic data
    // matching what the API layer returns for the corresponding result type.
    private const int StatusForbidden = 403;
    private const int StatusBadRequest = 400;

    public async Task<Result<OidcCallbackResponse>> Handle(
        OidcCallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        // --- Require the pre-auth session (bound to the browser via cookie) ---
        if (string.IsNullOrEmpty(command.SessionId))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "callback",
                HttpStatus = StatusForbidden
            });
            return Result<OidcCallbackResponse>.Forbidden("Missing pre-auth session.");
        }

        var session = await sessionStore.GetAsync(command.SessionId, cancellationToken);
        if (session == null)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "callback",
                SessionId = command.SessionId,
                HttpStatus = StatusForbidden
            });
            return Result<OidcCallbackResponse>.Forbidden("Pre-auth session expired or invalid.");
        }

        // --- Validate state matches stored value (CSRF protection) ---
        if (string.IsNullOrEmpty(command.State) || command.State != session.State)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "mismatched_state",
                Phase = "callback",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest
            });
            return Result<OidcCallbackResponse>.ValidationFailed("state", "State parameter mismatch.");
        }

        // --- Verify the session hasn't already been used (fail fast before the exchange) ---
        if (session.Phase != PreAuthSessionPhase.Created)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "callback",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest,
                ApiError = $"Phase={session.Phase}"
            });
            return Result<OidcCallbackResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Pre-auth session has already been used.");
        }

        // --- Exchange the authorization code using server-side session values.
        // code_verifier, redirectUri, and isStepUp are read from the pre-auth session —
        // never from the browser. ---
        var result = await exchangeService.ExchangeCodeAsync(
            command.Code,
            session.CodeVerifier,
            session.RedirectUri,
            session.IsStepUp,
            command.SessionId,
            cancellationToken);

        if (!result.Success)
        {
            // Exchange failures are logged by the exchange service with SessionId and IdP detail.
            return Result<OidcCallbackResponse>.DependencyFailed(
                ToDependencyFailedReason(result.FailureReason),
                result.Error ?? "Exchange failed.");
        }

        // --- Advance session to CallbackCompleted and store the callback token hash ---
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(result.CallbackToken!);
        var advanced = await sessionStore.TryAdvanceToCallbackCompletedAsync(
            command.SessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "callback",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusBadRequest
            });
            return Result<OidcCallbackResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Pre-auth session has already been used.");
        }

        logger.LogInformation(
            "OIDC Callback exchange succeeded: IsStepUp={IsStepUp}, Phone={MaskedPhone}, SessionId={SessionId}",
            session.IsStepUp,
            PiiMasker.MaskPhone(result.PhoneClaim),
            command.SessionId);

        return Result<OidcCallbackResponse>.Success(new OidcCallbackResponse(result.CallbackToken!));
    }

    /// <summary>
    /// Translates exchange failure categories into the Kernel dependency-failure vocabulary
    /// (the API layer maps these to 503 / 502 / 400 respectively).
    /// </summary>
    private static DependencyFailedReason ToDependencyFailedReason(OidcExchangeFailureReason? reason) =>
        reason switch
        {
            OidcExchangeFailureReason.NotConfigured => DependencyFailedReason.NotConfigured,
            OidcExchangeFailureReason.DiscoveryUnavailable => DependencyFailedReason.ConnectionFailed,
            OidcExchangeFailureReason.DiscoveryInvalid => DependencyFailedReason.ConnectionFailed,
            _ => DependencyFailedReason.BadRequest
        };
}
