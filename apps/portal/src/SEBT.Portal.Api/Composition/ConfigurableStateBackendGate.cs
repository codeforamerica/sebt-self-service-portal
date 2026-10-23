using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Composition;

internal static class ConfigurableStateBackendGate
{
    public const string MissingConfigMessage =
        "Feature flag use_configurable_state_backend is enabled but StateBackend:ConfigPath is not set. " +
        "Point ConfigPath at a YAML bundle or disable the flag.";

    public static async Task<bool> UseAdapterAsync(IFeatureManager featureManager, object? backend)
    {
        ArgumentNullException.ThrowIfNull(featureManager);

        if (!await featureManager.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend))
        {
            return false;
        }

        if (backend is null)
        {
            throw new InvalidOperationException(MissingConfigMessage);
        }

        return true;
    }
}
