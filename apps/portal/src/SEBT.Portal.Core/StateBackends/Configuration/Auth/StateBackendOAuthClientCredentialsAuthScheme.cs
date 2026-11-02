namespace SEBT.Portal.Core.StateBackends.Configuration.Auth;

public sealed record StateBackendOAuthClientCredentialsAuthScheme : StateBackendAuthScheme
{
    public required Uri TokenUrl { get; init; }

    /// <summary>Literal client id. Set exactly one of this or <see cref="ClientIdRef"/>.</summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Configuration key holding the client id, for ids that vary per environment. Set exactly
    /// one of this or <see cref="ClientId"/>.
    /// </summary>
    public string? ClientIdRef { get; init; }

    public required string ClientSecretRef { get; init; }
    public string? Scope { get; init; }
}
