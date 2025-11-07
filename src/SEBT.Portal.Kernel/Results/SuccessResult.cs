namespace SEBT.Portal.Kernel.Results;

public class SuccessResult() : Result(true)
{
    public override string Message => "The operation was successful.";
}

public class SuccessResult<T>(T value) : Result<T>(true)
{
    public override string Message => "The operation was successful.";

    public T Value => value;
    
    public override Result Map() => new SuccessResult();
}
