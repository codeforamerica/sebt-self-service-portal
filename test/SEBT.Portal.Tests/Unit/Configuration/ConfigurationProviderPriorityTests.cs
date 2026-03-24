using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using RichardSzalay.MockHttp;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

/// <summary>
/// Integration tests verifying the configuration provider priority chain:
///   1. appsettings.json (defaults) — lowest priority
///   2. State-specific JSON (appsettings.{State}.json) — middle priority
///   3. AWS AppConfig Agent — highest priority, overrides all
///
/// These tests build a real IConfiguration with multiple providers and verify
/// that later providers override earlier ones, matching the registration order
/// in Program.cs.
/// </summary>
public class ConfigurationProviderPriorityTests : IDisposable
{
    private const string AgentBaseUrl = "http://localhost:2772";
    private const string AppId = "test-app";
    private const string EnvId = "test-env";
    private const string FlagProfileId = "flag-profile";
    private const string SettingsProfileId = "settings-profile";

    private readonly MockHttpMessageHandler _mockHttp = new();

    [Fact]
    public async Task FeatureFlags_DefaultsUsedWhenAppConfigUnavailable()
    {
        var defaults = new Dictionary<string, string?>
        {
            ["FeatureManagement:test_feature"] = "true",
        };

        _mockHttp
            .When($"{AgentBaseUrl}/applications/{AppId}/environments/{EnvId}/configurations/{FlagProfileId}")
            .Respond(HttpStatusCode.InternalServerError);

        var config = BuildConfiguration(defaults, featureFlagProfileId: FlagProfileId);
        var featureManager = BuildFeatureManager(config);

        var isEnabled = await featureManager.IsEnabledAsync("test_feature");

        Assert.True(isEnabled);
    }

    [Fact]
    public void AppSettings_AppConfigOverridesDefaults()
    {
        var defaults = new Dictionary<string, string?>
        {
            ["OtpRateLimitSettings:PermitLimit"] = "5",
            ["OtpRateLimitSettings:WindowMinutes"] = "1.0",
        };

        var appConfigSettings = new
        {
            OtpRateLimitSettings = new { PermitLimit = 10 }
        };
        SetupMockEndpoint(SettingsProfileId, appConfigSettings);

        var config = BuildConfiguration(defaults, appSettingsProfileId: SettingsProfileId);

        var permitLimit = config["OtpRateLimitSettings:PermitLimit"];
        var windowMinutes = config["OtpRateLimitSettings:WindowMinutes"];

        Assert.Equal("10", permitLimit);
        Assert.Equal("1.0", windowMinutes);
    }

    [Fact]
    public void AppSettings_DefaultsUsedWhenAppConfigUnavailable()
    {
        var defaults = new Dictionary<string, string?>
        {
            ["OtpRateLimitSettings:PermitLimit"] = "5",
        };

        _mockHttp
            .When($"{AgentBaseUrl}/applications/{AppId}/environments/{EnvId}/configurations/{SettingsProfileId}")
            .Respond(HttpStatusCode.InternalServerError);

        var config = BuildConfiguration(defaults, appSettingsProfileId: SettingsProfileId);

        var permitLimit = config["OtpRateLimitSettings:PermitLimit"];

        Assert.Equal("5", permitLimit);
    }

    [Fact]
    public async Task FeatureFlags_AppConfigOverridesMultipleFlags()
    {
        var defaults = new Dictionary<string, string?>
        {
            ["FeatureManagement:flag_a"] = "false",
            ["FeatureManagement:flag_b"] = "true",
        };

        var appConfigFlags = new
        {
            flag_a = new { enabled = true },
            flag_b = new { enabled = false },
        };
        SetupMockEndpoint(FlagProfileId, appConfigFlags);

        var config = BuildConfiguration(defaults, featureFlagProfileId: FlagProfileId);
        var featureManager = BuildFeatureManager(config);

        var flagA = await featureManager.IsEnabledAsync("flag_a");
        var flagB = await featureManager.IsEnabledAsync("flag_b");

        Assert.True(flagA);
        Assert.False(flagB);
    }

    [Fact]
    public async Task DefaultsUsedWhenAppConfigNotConfigured()
    {
        // No AppConfig provider registered (DC scenario)
        var defaults = new Dictionary<string, string?>
        {
            ["FeatureManagement:test_feature"] = "true",
            ["OtpRateLimitSettings:PermitLimit"] = "5",
        };

        var config = BuildConfiguration(defaults);
        var featureManager = BuildFeatureManager(config);

        Assert.True(await featureManager.IsEnabledAsync("test_feature"));
        Assert.Equal("5", config["OtpRateLimitSettings:PermitLimit"]);
    }

    /// <summary>
    /// Builds an IConfigurationRoot mimicking the provider registration order in Program.cs:
    ///   1. In-memory defaults (simulates appsettings.json)
    ///   2. AppConfig feature flags provider (if profileId given)
    ///   3. AppConfig app settings provider (if profileId given)
    /// </summary>
    private IConfigurationRoot BuildConfiguration(
        Dictionary<string, string?> defaults,
        string? featureFlagProfileId = null,
        string? appSettingsProfileId = null)
    {
        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults);

        var httpClient = new HttpClient(_mockHttp)
        {
            BaseAddress = new Uri(AgentBaseUrl)
        };

        if (!string.IsNullOrEmpty(featureFlagProfileId))
        {
            builder.Add(new AppConfigAgentConfigurationSource
            {
                HttpClient = httpClient,
                Profile = new AppConfigAgentProfile
                {
                    BaseUrl = AgentBaseUrl,
                    ApplicationId = AppId,
                    EnvironmentId = EnvId,
                    ProfileId = featureFlagProfileId,
                    ReloadAfterSeconds = null,
                    IsFeatureFlag = true,
                },
            });
        }

        if (!string.IsNullOrEmpty(appSettingsProfileId))
        {
            builder.Add(new AppConfigAgentConfigurationSource
            {
                HttpClient = httpClient,
                Profile = new AppConfigAgentProfile
                {
                    BaseUrl = AgentBaseUrl,
                    ApplicationId = AppId,
                    EnvironmentId = EnvId,
                    ProfileId = appSettingsProfileId,
                    ReloadAfterSeconds = null,
                    IsFeatureFlag = false,
                },
            });
        }

        return builder.Build();
    }

    /// <summary>
    /// Creates an IFeatureManager backed by the given configuration, using the same
    /// AddFeatureManagement registration as Program.cs.
    /// </summary>
    private static IFeatureManager BuildFeatureManager(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddFeatureManagement(config.GetSection("FeatureManagement"));

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFeatureManager>();
    }

    private void SetupMockEndpoint(string profileId, object responseBody)
    {
        _mockHttp
            .When($"{AgentBaseUrl}/applications/{AppId}/environments/{EnvId}/configurations/{profileId}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(responseBody));
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }
}
