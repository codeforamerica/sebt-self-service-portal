namespace SEBT.Portal.Core.StateBackends.Configuration.Auth;

public sealed record StateBackendApiKeyAuthScheme : StateBackendAuthScheme
{
    public required string Header { get; init; }
    public required string KeyRef { get; init; }
}
