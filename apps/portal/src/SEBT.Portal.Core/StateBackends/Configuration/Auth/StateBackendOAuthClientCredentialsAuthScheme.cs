namespace SEBT.Portal.Core.StateBackends.Configuration.Auth;

public sealed record StateBackendOAuthClientCredentialsAuthScheme : StateBackendAuthScheme
{
    public required Uri TokenUrl { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecretRef { get; init; }
    public override AuthSchemes Scheme => AuthSchemes.OAuthClientCredentials;
}
