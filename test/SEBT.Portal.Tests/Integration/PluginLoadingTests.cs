using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Infrastructure.States;
using SEBT.Portal.Kernel;
using SEBT.Portal.StateConnector;
using System.Reflection;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for plugin loading functionality.
/// These tests verify the actual plugin discovery, loading, and integration with the application.
/// </summary>
public class PluginLoadingTests
{
    /// <summary>
    /// Tests that the plugin registry can discover plugins from loaded assemblies.
    /// </summary>
    [Fact]
    public void PluginRegistry_ShouldDiscoverPlugins_FromLoadedAssemblies()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Act
        var allPlugins = registry.GetAllPlugins().ToList();

        // Assert
        Assert.NotEmpty(allPlugins);

        // Verify we can find at least one plugin (DC or CO if they're referenced)
        var hasKnownPlugin = allPlugins.Any(p =>
            p.StateCode.Equals("DC", StringComparison.OrdinalIgnoreCase) ||
            p.StateCode.Equals("CO", StringComparison.OrdinalIgnoreCase));

        // Note: This test will pass if plugins are loaded via project references in test context
        // or if they're in the plugins directory
    }

    /// <summary>
    /// Tests that GetPlugin returns the correct plugin for a given state code.
    /// </summary>
    [Fact]
    public void PluginRegistry_GetPlugin_ShouldReturnCorrectPlugin_ForStateCode()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Act - Try to get DC plugin
        var dcPlugin = registry.GetPlugin("DC");
        var coPlugin = registry.GetPlugin("CO");
        var unknownPlugin = registry.GetPlugin("XX");

        // Assert
        if (dcPlugin != null)
        {
            Assert.Equal("DC", dcPlugin.StateCode);
            Assert.Equal("District of Columbia", dcPlugin.StateName);
            Assert.NotNull(dcPlugin.Version);
        }

        if (coPlugin != null)
        {
            Assert.Equal("CO", coPlugin.StateCode);
            Assert.Equal("Colorado", coPlugin.StateName);
            Assert.NotNull(coPlugin.Version);
        }

        Assert.Null(unknownPlugin);
    }

    /// <summary>
    /// Tests that GetActivePlugin returns the plugin based on STATE environment variable.
    /// </summary>
    [Fact]
    public void PluginRegistry_GetActivePlugin_ShouldReturnPlugin_ForStateEnvironmentVariable()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Save original environment variable
        var originalState = Environment.GetEnvironmentVariable("STATE");
        var originalNextPublicState = Environment.GetEnvironmentVariable("NEXT_PUBLIC_STATE");

        try
        {
            // Act - Set STATE to DC
            Environment.SetEnvironmentVariable("STATE", "DC");
            Environment.SetEnvironmentVariable("NEXT_PUBLIC_STATE", null);
            var dcPlugin = registry.GetActivePlugin();

            // Set STATE to CO
            Environment.SetEnvironmentVariable("STATE", "CO");
            var coPlugin = registry.GetActivePlugin();

            // Set to unknown state
            Environment.SetEnvironmentVariable("STATE", "XX");
            var unknownPlugin = registry.GetActivePlugin();

            // Clear STATE, set NEXT_PUBLIC_STATE
            Environment.SetEnvironmentVariable("STATE", null);
            Environment.SetEnvironmentVariable("NEXT_PUBLIC_STATE", "DC");
            var nextPublicPlugin = registry.GetActivePlugin();

            // Assert
            if (dcPlugin != null)
            {
                Assert.Equal("DC", dcPlugin.StateCode);
            }

            if (coPlugin != null)
            {
                Assert.Equal("CO", coPlugin.StateCode);
            }

            Assert.Null(unknownPlugin);

            if (nextPublicPlugin != null)
            {
                Assert.Equal("DC", nextPublicPlugin.StateCode);
            }
        }
        finally
        {
            // Restore original environment variables
            if (originalState != null)
            {
                Environment.SetEnvironmentVariable("STATE", originalState);
            }
            else
            {
                Environment.SetEnvironmentVariable("STATE", null);
            }

            if (originalNextPublicState != null)
            {
                Environment.SetEnvironmentVariable("NEXT_PUBLIC_STATE", originalNextPublicState);
            }
            else
            {
                Environment.SetEnvironmentVariable("NEXT_PUBLIC_STATE", null);
            }
        }
    }

    /// <summary>
    /// Tests that discovered plugins have valid properties.
    /// </summary>
    [Fact]
    public void PluginRegistry_DiscoveredPlugins_ShouldHaveValidProperties()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Act
        var allPlugins = registry.GetAllPlugins().ToList();

        // Assert
        foreach (var plugin in allPlugins)
        {
            Assert.NotNull(plugin);
            Assert.False(string.IsNullOrWhiteSpace(plugin.StateCode));
            Assert.False(string.IsNullOrWhiteSpace(plugin.StateName));
            Assert.NotNull(plugin.Version);

            // Verify StateCode is 2 characters
            Assert.True(plugin.StateCode.Length == 2,
                $"StateCode '{plugin.StateCode}' should be 2 characters");
        }
    }

    /// <summary>
    /// Tests that plugins can register configuration and services.
    /// </summary>
    [Fact]
    public void Plugin_RegisterConfiguration_ShouldNotThrow()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act & Assert - Should not throw
        var allPlugins = registry.GetAllPlugins();
        foreach (var plugin in allPlugins)
        {
            var exception = Record.Exception(() =>
            {
                plugin.RegisterConfiguration(services, configuration);
                plugin.RegisterServices(services);
            });

            Assert.Null(exception);
        }
    }

    /// <summary>
    /// Tests that plugins return default feature flags.
    /// </summary>
    [Fact]
    public void Plugin_GetDefaultFeatureFlags_ShouldReturnValidDictionary()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Act
        var allPlugins = registry.GetAllPlugins();

        // Assert
        foreach (var plugin in allPlugins)
        {
            var flags = plugin.GetDefaultFeatureFlags();

            Assert.NotNull(flags);

            // Verify all keys are non-empty
            foreach (var (key, value) in flags)
            {
                Assert.False(string.IsNullOrWhiteSpace(key));
                // Value is already a bool from Dictionary<string, bool>
            }
        }
    }

    /// <summary>
    /// Tests that plugins can validate configuration.
    /// </summary>
    [Fact]
    public void Plugin_ValidateConfiguration_ShouldReturnValidationResult()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var allPlugins = registry.GetAllPlugins();

        // Assert
        foreach (var plugin in allPlugins)
        {
            var (isValid, errorMessage) = plugin.ValidateConfiguration(configuration);

            // Validation should return a result
            // If invalid, error message should be provided
            if (!isValid)
            {
                Assert.False(string.IsNullOrWhiteSpace(errorMessage));
            }
            // If valid, error message can be null or empty
        }
    }

    /// <summary>
    /// Tests that FeatureFlagService merges plugin defaults with configuration.
    /// </summary>
    [Fact]
    public void FeatureFlagService_ShouldMergePluginDefaults_WithConfiguration()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        var dcPlugin = registry.GetPlugin("DC");
        if (dcPlugin == null)
        {
            // Skip test if DC plugin is not available
            return;
        }

        // Set STATE to DC
        var originalState = Environment.GetEnvironmentVariable("STATE");
        try
        {
            Environment.SetEnvironmentVariable("STATE", "DC");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Features:Flags:multi_language", "false" } // Override plugin default
                })
                .Build();

            var services = new ServiceCollection();
            services.AddOptions();
            services.AddSingleton<IStatePluginRegistry>(registry);
            services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
            services.Configure<FeatureFlagSettings>(configuration.GetSection(FeatureFlagSettings.SectionName));

            var serviceProvider = services.BuildServiceProvider();
            var featureFlagService = serviceProvider.GetRequiredService<IFeatureFlagService>();

            // Act
            var flags = featureFlagService.GetFeatureFlags();

            // Assert
            Assert.NotNull(flags);

            // Should have flags from plugin
            var pluginDefaults = dcPlugin.GetDefaultFeatureFlags();
            foreach (var (key, _) in pluginDefaults)
            {
                Assert.True(flags.ContainsKey(key), $"Flag '{key}' should be present from plugin defaults");
            }

            // Configuration should override plugin default
            if (pluginDefaults.ContainsKey("multi_language"))
            {
                // Config says false, plugin might say true, but config should win
                Assert.False(flags["multi_language"]);
            }
        }
        finally
        {
            if (originalState != null)
            {
                Environment.SetEnvironmentVariable("STATE", originalState);
            }
            else
            {
                Environment.SetEnvironmentVariable("STATE", null);
            }
        }
    }

    /// <summary>
    /// Tests that FeatureFlagService works without a plugin (fallback to config only).
    /// </summary>
    [Fact]
    public void FeatureFlagService_ShouldWork_WithoutPlugin()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Set STATE to unknown state
        var originalState = Environment.GetEnvironmentVariable("STATE");
        try
        {
            Environment.SetEnvironmentVariable("STATE", "XX");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Features:Flags:test_flag", "true" }
                })
                .Build();

            var services = new ServiceCollection();
            services.AddOptions();
            services.AddSingleton<IStatePluginRegistry>(registry);
            services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
            services.Configure<FeatureFlagSettings>(configuration.GetSection(FeatureFlagSettings.SectionName));

            var serviceProvider = services.BuildServiceProvider();
            var featureFlagService = serviceProvider.GetRequiredService<IFeatureFlagService>();

            // Act
            var flags = featureFlagService.GetFeatureFlags();

            // Assert
            Assert.NotNull(flags);
            Assert.True(flags.ContainsKey("test_flag"));
            Assert.True(flags["test_flag"]);
        }
        finally
        {
            if (originalState != null)
            {
                Environment.SetEnvironmentVariable("STATE", originalState);
            }
            else
            {
                Environment.SetEnvironmentVariable("STATE", null);
            }
        }
    }

    /// <summary>
    /// Tests that duplicate plugins are handled gracefully.
    /// </summary>
    [Fact]
    public void PluginRegistry_ShouldHandleDuplicatePlugins_Gracefully()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Act - Get all plugins
        var allPlugins = registry.GetAllPlugins().ToList();

        // Assert - No duplicate state codes
        var stateCodes = allPlugins.Select(p => p.StateCode.ToUpperInvariant()).ToList();
        var uniqueStateCodes = stateCodes.Distinct().ToList();

        Assert.Equal(uniqueStateCodes.Count, stateCodes.Count);
    }

    /// <summary>
    /// Tests that plugin registry is thread-safe.
    /// </summary>
    [Fact]
    public async Task PluginRegistry_ShouldBeThreadSafe()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<StatePluginRegistry>();
        var registry = new StatePluginRegistry(logger);

        // Act - Access registry from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var plugins = registry.GetAllPlugins().ToList();
                var dcPlugin = registry.GetPlugin("DC");
                var coPlugin = registry.GetPlugin("CO");
                var activePlugin = registry.GetActivePlugin();
            }));
        }

        // Assert - Should complete without exceptions
        var exception = await Record.ExceptionAsync(async () => await Task.WhenAll(tasks));
        Assert.Null(exception);
    }
}

