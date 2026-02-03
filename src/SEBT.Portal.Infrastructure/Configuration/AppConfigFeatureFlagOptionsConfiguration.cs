using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Post-configures AppConfigFeatureFlagSettings by extracting feature flags from the AppConfig section.
/// </summary>
public class AppConfigFeatureFlagOptionsConfiguration : IPostConfigureOptions<AppConfigFeatureFlagSettings>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppConfigFeatureFlagOptionsConfiguration> _logger;

    public AppConfigFeatureFlagOptionsConfiguration(
        IConfiguration configuration,
        ILogger<AppConfigFeatureFlagOptionsConfiguration> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void PostConfigure(string? name, AppConfigFeatureFlagSettings options)
    {
        // Check if AppConfig Agent is configured
        var agentSection = _configuration.GetSection("AppConfig:Agent");
        var hasAgentConfig = agentSection.Exists() &&
            !string.IsNullOrEmpty(agentSection.GetValue<string>("ApplicationId")) &&
            !string.IsNullOrEmpty(agentSection.GetValue<string>("EnvironmentId")) &&
            !string.IsNullOrEmpty(agentSection.GetValue<string>("ProfileId"));

        // Check legacy AppConfig section
        var appConfigSection = _configuration.GetSection(AppConfigFeatureFlagSettings.SectionName);
        var legacyEnabled = false;

        if (appConfigSection.Exists())
        {
            try
            {
                legacyEnabled = appConfigSection.GetValue<bool>("Enabled", false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read AppConfig Enabled setting, assuming disabled");
            }
        }

        // AppConfig is enabled if either agent is configured or legacy enabled flag is set
        options.Enabled = hasAgentConfig || legacyEnabled;

        if (!options.Enabled)
        {
            return;
        }

        // Try to read feature flags from AppConfig configuration source
        var appConfigFeatureSection = _configuration.GetSection("FeatureManagement:AppConfig:Features");

        if (appConfigFeatureSection.Exists())
        {
            foreach (var child in appConfigFeatureSection.GetChildren())
            {
                if (child.Value != null && bool.TryParse(child.Value, out var boolValue))
                {
                    options.Features[child.Key] = boolValue;
                }
                else
                {
                    try
                    {
                        var enabledValue = child.GetValue<bool?>("Enabled");
                        if (enabledValue.HasValue)
                        {
                            options.Features[child.Key] = enabledValue.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to parse AppConfig feature flag {FeatureName}", child.Key);
                    }
                }
            }
        }
        else
        {
            // If AppConfig is enabled but no features section exists,
            // try to read from the main FeatureManagement section which might be populated by AppConfig Agent
            var featureManagementSection = _configuration.GetSection("FeatureManagement");
            foreach (var child in featureManagementSection.GetChildren())
            {
                // Skip AppConfig configuration section itself
                if (child.Key.Equals("AppConfig", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (child.Value != null && bool.TryParse(child.Value, out var boolValue))
                {
                    options.Features[child.Key] = boolValue;
                }
            }
        }
    }
}
