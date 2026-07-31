using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;

namespace SEBT.Portal.Infrastructure.StateBackends;

/// <summary>
/// Composes the config-driven state-backend stack and exposes it per Core port. Composition is
/// lazy — nothing is touched until the first port read, so registration is free while the feature
/// flag is off. Ports the config does not declare get a throwing <see cref="UnsupportedStateBackendOperation"/>.
/// </summary>
public sealed class ConfigurableStateBackendPorts
{
    /// <summary>Configuration key holding the path to the state's backend YAML config file.</summary>
    public const string ConfigPathKey = "StateBackend:ConfigPath";

    private readonly Lazy<ResolvedPorts> _ports;

    public ConfigurableStateBackendPorts(
        IConfiguration configuration, IStateBackendSecretResolver secretResolver)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(secretResolver);

        _ports = new Lazy<ResolvedPorts>(
            () => Build(configuration, secretResolver),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ICardReplacementBackend CardReplacement => _ports.Value.CardReplacement;

    public IAddressUpdateBackend AddressUpdate => _ports.Value.AddressUpdate;

    public IEnrollmentCheckBackend EnrollmentCheck => _ports.Value.EnrollmentCheck;

    public IHouseholdLookupBackend HouseholdLookup => _ports.Value.HouseholdLookup;

    private sealed record ResolvedPorts(
        ICardReplacementBackend CardReplacement,
        IAddressUpdateBackend AddressUpdate,
        IEnrollmentCheckBackend EnrollmentCheck,
        IHouseholdLookupBackend HouseholdLookup);

    private static ResolvedPorts Build(
        IConfiguration configuration, IStateBackendSecretResolver secretResolver)
    {
        string? configPath = configuration[ConfigPathKey];
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new InvalidOperationException(
                $"The configurable state backend is enabled but '{ConfigPathKey}' is not set. " +
                "Point it at the state's backend YAML configuration file.");
        }

        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException(
                "The configurable state backend is enabled but no config file exists at " +
                $"'{configPath}' (from '{ConfigPathKey}').");
        }

        // The loader validates the config shape and fails loud on anything malformed.
        StateBackendConfiguration config =
            StateBackendConfigurationLoader.Load(File.ReadAllText(configPath));

        var backend = new ConfigurableStateBackend(config, BuildHttpClient(config, secretResolver));

        // Undeclared operations bind to the throwing null-object; declared ones share the backend.
        var unsupported = new UnsupportedStateBackendOperation();
        return new ResolvedPorts(
            config.Operations.CardReplacement is not null ? backend : unsupported,
            config.Operations.AddressUpdate is not null ? backend : unsupported,
            config.Operations.EnrollmentCheck is not null ? backend : unsupported,
            config.Operations.HouseholdLookup is not null ? backend : unsupported);
    }

    // The config's auth scheme as a DelegatingHandler in front of the transport. Hand-built once
    // for the singleton backend — a process-lifetime client gains nothing from IHttpClientFactory's
    // handler rotation.
    private static HttpClient BuildHttpClient(
        StateBackendConfiguration config, IStateBackendSecretResolver secretResolver)
    {
        HttpMessageHandler handler = config.Auth switch
        {
            StateBackendApiKeyAuthScheme apiKey =>
                new StateBackendApiKeyAuthHandler(apiKey, secretResolver)
                {
                    InnerHandler = CreateTransportHandler(),
                },
            StateBackendOAuthClientCredentialsAuthScheme clientCredentials =>
                new StateBackendOAuthClientCredentialsAuthHandler(
                    clientCredentials,
                    secretResolver,
                    tokenClient: new HttpClient(CreateTransportHandler()))
                {
                    InnerHandler = CreateTransportHandler(),
                },
            _ => throw new NotSupportedException(
                $"Unsupported state-backend auth scheme '{config.Auth.GetType().Name}'."),
        };

        // The backend sets BaseAddress from the config's baseUrl.
        return new HttpClient(handler);
    }

    // Bounded connection lifetime so a process-lifetime client still observes DNS changes.
    private static SocketsHttpHandler CreateTransportHandler() =>
        new() { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
}
