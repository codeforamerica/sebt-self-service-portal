namespace SEBT.Portal.Core.StateBackends.Configuration.Auth;

/// <summary>Auth scheme for the state backend. Discriminated by <see cref="Scheme"/> in YAML.</summary>
public abstract record StateBackendAuthScheme
{
    /// <summary>
    /// YAML discriminator (<c>api_key</c> / <c>client_credentials</c>). Bound so the loader
    /// can reject unknown properties without ignoring the discriminator key itself.
    /// </summary>
    public string? Scheme { get; init; }
}
