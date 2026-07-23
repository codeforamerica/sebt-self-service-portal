namespace SEBT.Portal.Core.StateBackends.Configuration.Auth;

public abstract record StateBackendAuthScheme
{
    public abstract AuthSchemes Scheme { get; }
}
