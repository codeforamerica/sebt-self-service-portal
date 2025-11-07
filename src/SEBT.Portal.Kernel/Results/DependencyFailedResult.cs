namespace SEBT.Portal.Kernel.Results;

public class DependencyFailedResult(DependencyFailedReason reason, string? message) 
    : Result(false)
{
    public DependencyFailedReason Reason => reason;
    
    public override string Message => message ?? Reason.ToString();
}

public class DependencyFailedResult<T>(DependencyFailedReason reason, string? message) 
    : Result<T>(false)
{
    public DependencyFailedReason Reason => reason;
    
    public override string Message => message ?? Reason.ToString();
    
    public override Result Map() => new DependencyFailedResult(Reason, Message);
}
