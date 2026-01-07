using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Tests.Unit.Services;

public class FeatureFlagServiceTests
{
    private readonly IOptions<FeatureFlagSettings> _options = Substitute.For<IOptions<FeatureFlagSettings>>();
    private readonly IStatePluginRegistry _pluginRegistry = Substitute.For<IStatePluginRegistry>();
    private readonly FeatureFlagService _featureFlagService;

    public FeatureFlagServiceTests()
    {
        var settings = new FeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>
            {
                { "multi_language", true },
                { "advanced_search", false },
                { "experimental_ui", true }
            }
        };
        _options.Value.Returns(settings);
        _pluginRegistry.GetActivePlugin().Returns((SEBT.Portal.StateConnector.IStatePlugin?)null);
        _featureFlagService = new FeatureFlagService(_options, _pluginRegistry);
    }

    [Fact]
    public void GetFeatureFlags_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        // Act
        var flags = _featureFlagService.GetFeatureFlags();

        // Assert
        Assert.True(flags.ContainsKey("multi_language"));
        Assert.True(flags["multi_language"]);
    }

    [Fact]
    public void GetFeatureFlags_WhenFlagIsDisabled_ShouldReturnFalse()
    {
        // Act
        var flags = _featureFlagService.GetFeatureFlags();

        // Assert
        Assert.True(flags.ContainsKey("advanced_search"));
        Assert.False(flags["advanced_search"]);
    }

    [Fact]
    public void GetFeatureFlags_WhenUnknownFlag_ShouldNotBeIncluded()
    {
        // Act
        var flags = _featureFlagService.GetFeatureFlags();

        // Assert
        Assert.False(flags.ContainsKey("unknown_feature"));
    }

    [Fact]
    public void GetFeatureFlags_ShouldReturnCopyOfFlags()
    {
        // Act
        var flags1 = _featureFlagService.GetFeatureFlags();
        var flags2 = _featureFlagService.GetFeatureFlags();

        // Assert - Should be equal but not the same reference
        Assert.Equal(flags1, flags2);
        Assert.NotSame(flags1, flags2);
    }

    [Fact]
    public void GetFeatureFlags_WhenEmptyConfiguration_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var emptySettings = new FeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>()
        };
        var emptyOptions = Substitute.For<IOptions<FeatureFlagSettings>>();
        emptyOptions.Value.Returns(emptySettings);
        var emptyPluginRegistry = Substitute.For<IStatePluginRegistry>();
        emptyPluginRegistry.GetActivePlugin().Returns((SEBT.Portal.StateConnector.IStatePlugin?)null);
        var service = new FeatureFlagService(emptyOptions, emptyPluginRegistry);

        // Act
        var flags = service.GetFeatureFlags();

        // Assert
        Assert.NotNull(flags);
        Assert.Empty(flags);
    }

    [Fact]
    public void GetFeatureFlags_ShouldNotAllowExternalModification()
    {
        // Act
        var flags = _featureFlagService.GetFeatureFlags();
        flags.Add("new_flag", true);

        // Act again - should not include the modification
        var flags2 = _featureFlagService.GetFeatureFlags();

        // Assert
        Assert.False(flags2.ContainsKey("new_flag"));
    }
}

