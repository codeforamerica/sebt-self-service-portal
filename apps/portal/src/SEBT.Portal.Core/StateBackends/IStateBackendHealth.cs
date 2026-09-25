namespace SEBT.Portal.Core.StateBackends;

public interface IStateBackendHealth
{
    Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record StateBackendHealth(bool IsHealthy);
