using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Infrastructure.StateBackends.Auth;

/// <summary>
/// Resolves secret references against <see cref="IConfiguration"/>, so secrets ride the same
/// pipeline as the rest of runtime config (environment variables, per-state overlays, AppConfig).
/// A missing or empty key fails loud here — a silently empty credential would only surface later
/// as an opaque auth failure on the first backend call.
/// </summary>
public sealed class ConfigurationStateBackendSecretResolver : IStateBackendSecretResolver
{
    private readonly IConfiguration _configuration;

    public ConfigurationStateBackendSecretResolver(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;
    }

    public string Resolve(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        string? value = _configuration[reference];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"State-backend secret reference '{reference}' resolved to no value. " +
                "Set that configuration key (e.g. via an environment variable) to the secret it names.");
        }

        return value;
    }
}
