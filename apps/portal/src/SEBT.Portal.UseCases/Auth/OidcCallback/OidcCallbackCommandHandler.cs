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
///
/// Failure log entries derive their <c>HttpStatus</c> from
/// <see cref="OidcResultHttpStatus"/> — the same table the API layer maps responses
/// from — so the logged status is always the returned status.
/// </summary>
public class OidcCallbackCommandHandler(
    IPreAuthSessionStore sessionStore,
    IOidcExchangeService exchangeService,
    IOidcCallbackFailureLogger callbackFailureLogger,
    ILogger<OidcCallbackCommandHandler> logger)
    : ICommandHandler<OidcCallbackCommand, OidcCallbackResponse>
{
    public async Task<Result<OidcCallbackResponse>> Handle(
        OidcCallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        // --- Require the pre-auth session (bound to the browser via cookie) ---
        if (string.IsNullOrEmpty(command.SessionId))
        {
            var failure = Result<OidcCallbackResponse>.Forbidden("Missing pre-auth session.");
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "callback",
                HttpStatus = OidcResultHttpStatus.For(failure)
            });
            return failure;
        }

        var session = await sessionStore.GetAsync(command.SessionId, cancellationToken);
        if (session == null)
        {
            var failure = Result<OidcCallbackResponse>.Forbidden("Pre-auth session expired or invalid.");
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "callback",
                SessionId = command.SessionId,
                HttpStatus = OidcResultHttpStatus.For(failure)
            });
            return failure;
        }

        // --- Validate state matches stored value (CSRF protection) ---
        if (string.IsNullOrEmpty(command.State) || command.State != session.State)
        {
            var failure = Result<OidcCallbackResponse>.ValidationFailed(
                "state", "State parameter mismatch.");
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "mismatched_state",
                Phase = "callback",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = OidcResultHttpStatus.For(failure)
            });
            return failure;
        }

        // --- Verify the session hasn't already been used (fail fast before the exchange) ---
        if (session.Phase != PreAuthSessionPhase.Created)
        {
            var failure = Result<OidcCallbackResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Pre-auth session has already been used.");
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "callback",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = OidcResultHttpStatus.For(failure),
                ApiError = $"Phase={session.Phase}"
            });
            return failure;
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
            var failure = Result<OidcCallbackResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Pre-auth session has already been used.");
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "callback",
                SessionId = command.SessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = OidcResultHttpStatus.For(failure)
            });
            return failure;
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
    /// (the API layer maps these to 503 / 502 / 400 via <see cref="OidcResultHttpStatus"/>).
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
