namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public abstract record StateBackendOperationConfig()
{
    public required StateBackendHttpMethod Method { get; init; }

    public required string Path { get; init; }
};
