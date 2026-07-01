using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Post-configures FeatureManagementSettings by extracting feature flags from the FeatureManagement section,
/// excluding the AppConfig subsection.
/// </summary>
public class FeatureManagementOptionsConfiguration : IPostConfigureOptions<FeatureManagementSettings>
{
    private readonly IConfiguration _configuration;

    public FeatureManagementOptionsConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void PostConfigure(string? name, FeatureManagementSettings options)
    {
        var featureManagementSection = _configuration.GetSection(FeatureManagementSettings.SectionName);

        if (featureManagementSection.Exists())
        {
            foreach (var child in featureManagementSection.GetChildren())
            {
                // Skip AppConfig configuration section itself
                if (child.Key.Equals("AppConfig", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (child.Value != null && bool.TryParse(child.Value, out var boolValue))
                {
                    options.Flags[child.Key] = boolValue;
                }
            }
        }
    }
}
