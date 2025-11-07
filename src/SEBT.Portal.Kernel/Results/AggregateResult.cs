namespace SEBT.Portal.Kernel.Results;

public class AggregateResult(IReadOnlyList<Result> results)
    : Result(results.All(r => r.IsSuccess))
{
    public IReadOnlyList<Result> Results { get; } = results;

    public override string Message => "Aggregate result of multiple results";
}
