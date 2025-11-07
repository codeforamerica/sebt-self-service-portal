namespace SEBT.Portal.Kernel.Results;

public class PreconditionFailedResult(PreconditionFailedReason reason, string? message) : Result(false)
{
    public PreconditionFailedReason Reason { get; } = reason;

    public override string Message => message ?? Reason.ToMessage();
}

public class PreconditionFailedResult<T>(PreconditionFailedReason reason, string? message) : Result<T>(false)
{
    public PreconditionFailedReason Reason { get; } = reason;

    public override string Message => message ?? Reason.ToMessage();
    
    public override Result Map() => new PreconditionFailedResult(Reason, Message);
}
