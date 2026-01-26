using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Extension methods for adding AWS AppConfig Agent configuration provider.
/// </summary>
public static class AppConfigAgentConfigurationExtensions
{
    /// <summary>
    /// Adds AWS AppConfig Agent as a configuration source.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="baseUrl">Base URL of the AppConfig Agent (default: http://localhost:2772).</param>
    /// <param name="applicationId">Application identifier.</param>
    /// <param name="environmentId">Environment identifier.</param>
    /// <param name="profileId">Configuration profile identifier.</param>
    /// <param name="reloadAfterSeconds">Reload interval in seconds (default: 90).</param>
    /// <param name="isFeatureFlag">Whether this is a feature flag profile (default: true).</param>
    /// <param name="logger">Optional logger for the provider.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddAppConfigAgent(
        this IConfigurationBuilder builder,
        string baseUrl,
        string applicationId,
        string environmentId,
        string profileId,
        int? reloadAfterSeconds = 90,
        bool isFeatureFlag = true,
        ILogger<AppConfigAgentConfigurationProvider>? logger = null)
    {
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = baseUrl,
            ApplicationId = applicationId,
            EnvironmentId = environmentId,
            ProfileId = profileId,
            ReloadAfterSeconds = reloadAfterSeconds,
            IsFeatureFlag = isFeatureFlag
        };

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        var source = new AppConfigAgentConfigurationSource
        {
            HttpClient = httpClient,
            Profile = profile,
            Logger = logger
        };
        builder.Add(source);

        return builder;
    }

    /// <summary>
    /// Adds AWS AppConfig Agent as a configuration source from configuration section.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="sectionName">Name of the configuration section (default: "AppConfig:Agent").</param>
    /// <param name="logger">Optional logger for the provider.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddAppConfigAgent(
        this IConfigurationBuilder builder,
        string sectionName = "AppConfig:Agent",
        ILogger<AppConfigAgentConfigurationProvider>? logger = null)
    {
        var config = builder.Build();
        var section = config.GetSection(sectionName);

        if (!section.Exists())
        {
            return builder;
        }

        var baseUrl = section.GetValue<string>("BaseUrl") ?? "http://localhost:2772";
        var applicationId = section.GetValue<string>("ApplicationId");
        var environmentId = section.GetValue<string>("EnvironmentId");
        var profileId = section.GetValue<string>("ProfileId");
        var reloadAfterSeconds = section.GetValue<int?>("ReloadAfterSeconds") ?? 90;
        var isFeatureFlag = section.GetValue<bool?>("IsFeatureFlag") ?? true;

        if (string.IsNullOrEmpty(applicationId) || string.IsNullOrEmpty(environmentId) || string.IsNullOrEmpty(profileId))
        {
            logger?.LogWarning(
                "AppConfig Agent configuration section '{SectionName}' is missing required values (ApplicationId, EnvironmentId, or ProfileId). Agent provider will not be registered.",
                sectionName);
            return builder;
        }

        return builder.AddAppConfigAgent(
            baseUrl,
            applicationId,
            environmentId,
            profileId,
            reloadAfterSeconds,
            isFeatureFlag,
            logger);
    }
}
