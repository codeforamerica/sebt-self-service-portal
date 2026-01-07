using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.StateConnector;

namespace SEBT.Portal.Tests.Unit.Services;

public class FeatureFlagServiceWithPluginTests
{
    private readonly IOptions<FeatureFlagSettings> _options = Substitute.For<IOptions<FeatureFlagSettings>>();
    private readonly IStatePluginRegistry _pluginRegistry = Substitute.For<IStatePluginRegistry>();
    private readonly FeatureFlagService _featureFlagService;

    public FeatureFlagServiceWithPluginTests()
    {
        var settings = new FeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>()
        };
        _options.Value.Returns(settings);
        _featureFlagService = new FeatureFlagService(_options, _pluginRegistry);
    }

    [Fact]
    public void GetFeatureFlags_WithDCPlugin_ShouldReturnPluginDefaults()
    {
        // Arrange
        var dcPlugin = new DcTestPlugin();
        _pluginRegistry.GetActivePlugin().Returns(dcPlugin);

        // Act
        var flags = _featureFlagService.GetFeatureFlags();

        // Assert
        Assert.True(flags.ContainsKey("multi_language"));
        Assert.True(flags["multi_language"]);
        Assert.True(flags.ContainsKey("advanced_search"));
        Assert.False(flags["advanced_search"]);
        Assert.True(flags.ContainsKey("experimental_ui"));
        Assert.True(flags["experimental_ui"]);
    }

    [Fact]
    public void GetFeatureFlags_WithCOPlugin_ShouldReturnPluginDefaults()
    {
        // Arrange
        var coPlugin = new CoTestPlugin();
        _pluginRegistry.GetActivePlugin().Returns(coPlugin);

        // Act
        var flags = _featureFlagService.GetFeatureFlags();

        // Assert
        Assert.True(flags.ContainsKey("multi_language"));
        Assert.False(flags["multi_language"]);
        Assert.True(flags.ContainsKey("advanced_search"));
        Assert.True(flags["advanced_search"]);
        Assert.True(flags.ContainsKey("experimental_ui"));
        Assert.False(flags["experimental_ui"]);
    }

    [Fact]
    public void GetFeatureFlags_WithPluginAndConfig_ConfigShouldOverridePluginDefaults()
    {
        // Arrange
        var dcPlugin = new DcTestPlugin();
        _pluginRegistry.GetActivePlugin().Returns(dcPlugin);

        var settings = new FeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>
            {
                { "multi_language", false }, // Override plugin default (true -> false)
                { "advanced_search", true }   // Override plugin default (false -> true)
            }
        };
        _options.Value.Returns(settings);
        var service = new FeatureFlagService(_options, _pluginRegistry);

        // Act
        var flags = service.GetFeatureFlags();

        // Assert - Config should override plugin defaults
        Assert.True(flags.ContainsKey("multi_language"));
        Assert.False(flags["multi_language"]); // Overridden by config
        Assert.True(flags.ContainsKey("advanced_search"));
        Assert.True(flags["advanced_search"]); // Overridden by config
        Assert.True(flags.ContainsKey("experimental_ui"));
        Assert.True(flags["experimental_ui"]); // From plugin (not in config)
    }

    [Fact]
    public void GetFeatureFlags_WithoutPlugin_ShouldReturnOnlyConfig()
    {
        // Arrange
        _pluginRegistry.GetActivePlugin().Returns((IStatePlugin?)null);

        var settings = new FeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>
            {
                { "multi_language", true }
            }
        };
        _options.Value.Returns(settings);
        var service = new FeatureFlagService(_options, _pluginRegistry);

        // Act
        var flags = service.GetFeatureFlags();

        // Assert
        Assert.True(flags.ContainsKey("multi_language"));
        Assert.True(flags["multi_language"]);
        Assert.False(flags.ContainsKey("advanced_search")); // Not in config, no plugin
        Assert.False(flags.ContainsKey("experimental_ui")); // Not in config, no plugin
    }

    // Test plugin implementations
    private class DcTestPlugin : IStatePlugin
    {
        public string StateCode => "DC";
        public string StateName => "District of Columbia";
        public Version Version => new(1, 0, 0);

        public void RegisterConfiguration(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration) { }
        public void RegisterServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }

        public Dictionary<string, bool> GetDefaultFeatureFlags()
        {
            return new Dictionary<string, bool>
            {
                { "multi_language", true },
                { "advanced_search", false },
                { "experimental_ui", true }
            };
        }

        public (bool IsValid, string? ErrorMessage) ValidateConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            return (true, null);
        }
    }

    private class CoTestPlugin : IStatePlugin
    {
        public string StateCode => "CO";
        public string StateName => "Colorado";
        public Version Version => new(1, 0, 0);

        public void RegisterConfiguration(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration) { }
        public void RegisterServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }

        public Dictionary<string, bool> GetDefaultFeatureFlags()
        {
            return new Dictionary<string, bool>
            {
                { "multi_language", false },
                { "advanced_search", true },
                { "experimental_ui", false }
            };
        }

        public (bool IsValid, string? ErrorMessage) ValidateConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            return (true, null);
        }
    }
}


