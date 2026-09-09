using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Single source for the HTTP status the API layer returns for each OIDC failure.
/// The controller derives response status codes from this table, and the handlers
/// derive the pass-through <see cref="OidcCallbackFailureLogEntry.HttpStatus"/>
/// values from the same table — so the off-boarding dashboards can never drift
/// from what the API actually returned.
/// </summary>
public static class OidcResultHttpStatus
{
    public const int BadRequest = 400;
    public const int Forbidden = 403;
    public const int BadGateway = 502;
    public const int ServiceUnavailable = 503;

    /// <summary>
    /// The status the API returns for a failed OIDC handler result:
    /// forbidden → 403; not configured → 503; upstream IdP unreachable → 502;
    /// validation, precondition, and anything else → 400.
    /// </summary>
    public static int For<T>(Result<T> result) => result switch
    {
        ForbiddenResult<T> => Forbidden,
        DependencyFailedResult<T> { Reason: DependencyFailedReason.NotConfigured } => ServiceUnavailable,
        DependencyFailedResult<T> { Reason: DependencyFailedReason.ConnectionFailed } => BadGateway,
        _ => BadRequest
    };

    /// <summary>
    /// The status the API ultimately returns for a code-exchange failure, for log
    /// entries written before the failure is translated into a handler result.
    /// Mirrors <see cref="For{T}"/> composed with the handler's reason translation.
    /// </summary>
    public static int For(OidcExchangeFailureReason reason) => reason switch
    {
        OidcExchangeFailureReason.NotConfigured => ServiceUnavailable,
        OidcExchangeFailureReason.DiscoveryUnavailable => BadGateway,
        OidcExchangeFailureReason.DiscoveryInvalid => BadGateway,
        _ => BadRequest
    };
}
