namespace SEBT.Portal.Kernel.Results;

public enum PreconditionFailedReason
{
    NotFound = 1,

    ConcurrencyMismatch = 2,

    Conflict = 3,

    /// <summary>
    /// The requested action is not permitted for this account based on configuration policy.
    /// </summary>
    NotAllowed = 4,
}

internal static class PreconditionFailedReasonExtensions
{
    public static string ToMessage(this PreconditionFailedReason reason) => reason switch
    {
        PreconditionFailedReason.NotFound => "A requested resource was not found.",
        PreconditionFailedReason.ConcurrencyMismatch => "A concurrency mismatch occurred.",
        PreconditionFailedReason.Conflict => "A conflict occurred with the current state of the resource.",
        PreconditionFailedReason.NotAllowed => "The requested action is not permitted for this account.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };
}
