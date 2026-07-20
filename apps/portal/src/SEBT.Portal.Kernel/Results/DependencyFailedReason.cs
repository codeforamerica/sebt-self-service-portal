namespace SEBT.Portal.Kernel.Results;

public enum DependencyFailedReason
{
    ConnectionFailed,
    Timeout,
    Authentication,
    BadRequest,
    ServiceUnavailable,
    NotConfigured
}
