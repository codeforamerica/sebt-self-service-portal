using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;

namespace SEBT.Portal.Infrastructure.StateBackends;

/// <summary>
/// Composition-root registration for the config-driven state backend. Loads YAML when
/// <c>StateBackend:ConfigPath</c> is set; traffic still goes through the MEF plugins until
/// <see cref="SEBT.Portal.Core.AppSettings.FeatureFlags.UseConfigurableStateBackend"/> is enabled.
/// </summary>
public static class StateBackendServiceCollectionExtensions
{
    public const string ConfigPathKey = "StateBackend:ConfigPath";

    internal const string TokenHttpClientName = "StateBackendToken";

    public static IServiceCollection AddConfigurableStateBackend(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IStateBackendSecretResolver, ConfigurationStateBackendSecretResolver>();

        string? configPath = configuration[ConfigPathKey];
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return services;
        }

        string resolvedPath = ResolveConfigPath(configPath, contentRootPath);
        if (!File.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"StateBackend:ConfigPath '{resolvedPath}' does not exist. " +
                "Point it at a YAML bundle or leave it empty to keep the MEF plugin path.");
        }

        StateBackendConfiguration backendConfig = StateBackendConfigurationLoader.Load(
            File.ReadAllText(resolvedPath));
        services.AddSingleton(backendConfig);

        services.AddHttpClient(TokenHttpClientName);

        services.AddSingleton(sp =>
        {
            IStateBackendSecretResolver secrets = sp.GetRequiredService<IStateBackendSecretResolver>();
            HttpClient tokenClient = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(TokenHttpClientName);
            TimeProvider timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            DelegatingHandler auth = CreateAuthHandler(backendConfig.Auth, secrets, tokenClient, timeProvider);
            auth.InnerHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };

            var httpClient = new HttpClient(auth, disposeHandler: true)
            {
                BaseAddress = backendConfig.BaseUrl,
                Timeout = TimeSpan.FromSeconds(30),
            };

            return new ConfigurableStateBackend(backendConfig, httpClient);
        });

        services.AddSingleton<IHouseholdLookupBackend>(sp => sp.GetRequiredService<ConfigurableStateBackend>());
        services.AddSingleton<ICardReplacementBackend>(sp => sp.GetRequiredService<ConfigurableStateBackend>());
        services.AddSingleton<IAddressUpdateBackend>(sp => sp.GetRequiredService<ConfigurableStateBackend>());
        services.AddSingleton<IEnrollmentCheckBackend>(sp => sp.GetRequiredService<ConfigurableStateBackend>());
        services.AddSingleton<IStateBackendHealth>(sp => sp.GetRequiredService<ConfigurableStateBackend>());

        return services;
    }

    internal static string ResolveConfigPath(string configPath, string? contentRootPath)
    {
        if (Path.IsPathRooted(configPath) || string.IsNullOrWhiteSpace(contentRootPath))
        {
            return configPath;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configPath));
    }

    private static DelegatingHandler CreateAuthHandler(
        StateBackendAuthScheme scheme,
        IStateBackendSecretResolver secrets,
        HttpClient tokenClient,
        TimeProvider timeProvider) =>
        scheme switch
        {
            StateBackendApiKeyAuthScheme apiKey => new StateBackendApiKeyAuthHandler(apiKey, secrets),
            StateBackendOAuthClientCredentialsAuthScheme oauth =>
                new StateBackendOAuthClientCredentialsAuthHandler(oauth, secrets, tokenClient, timeProvider),
            _ => throw new InvalidOperationException(
                $"Unsupported state-backend auth scheme '{scheme.GetType().Name}'."),
        };
}
