using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.FeatureManagement;
using RichardSzalay.MockHttp;
using SEBT.Portal.Infrastructure.Configuration;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Configuration;

/// <summary>
/// Reproduces the real deployment symptom at the host-composition level: the AppConfig
/// provider lives inside the host's ConfigurationManager, and consumers (IFeatureManager,
/// IOptionsMonitor) refresh only when the configuration ROOT raises a change token. These
/// tests prove whether a provider reload propagates all the way to those consumers — which
/// the plain-provider unit tests cannot, because they never build a host.
/// </summary>
public class AppConfigAgentHotReloadHostTests
{
    private const string FeatureFlagEndpoint =
        "http://localhost:2772/applications/test-app/environments/test-env/configurations/flags";

    private static AppConfigAgentProfile FlagProfile() => new()
    {
        BaseUrl = "http://localhost:2772",
        ApplicationId = "test-app",
        EnvironmentId = "test-env",
        ProfileId = "flags",
        IsFeatureFlag = true
    };

    private static (MockHttpMessageHandler handler, HttpClient client) MakeToggleClient()
    {
        var handler = new MockHttpMessageHandler();
        var callCount = 0;
        handler
            .When(FeatureFlagEndpoint)
            .Respond(_ =>
            {
                callCount++;
                var enabled = callCount == 1; // first fetch true, every fetch after false
                var json = JsonSerializer.Serialize(new { enable_beta_banner = new { enabled } });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2772") };
        return (handler, client);
    }

    [Fact]
    public async Task ProviderReload_AfterHostBuild_PropagatesToFeatureManager()
    {
        // Arrange — compose a host the way Program.cs does: provider added to the
        // ConfigurationManager, then FeatureManagement reads from that same config root.
        var (_, client) = MakeToggleClient();
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        ((IConfigurationBuilder)builder.Configuration).Add(new AppConfigAgentConfigurationSource
        {
            HttpClient = client,
            Profile = FlagProfile()
        });
        builder.Services.AddFeatureManagement();

        var app = builder.Build();
        var provider = ((IConfigurationRoot)app.Configuration)
            .Providers
            .OfType<AppConfigAgentConfigurationProvider>()
            .Single();
        var featureManager = app.Services.GetRequiredService<IFeatureManager>();

        // Sanity: the initial load is visible through the feature manager.
        Assert.True(await featureManager.IsEnabledAsync("enable_beta_banner"));

        // Act — AppConfig flips the flag off; the background service reloads the provider.
        await provider.ReloadAsync();

        // Assert — the running app must now see the flag as disabled.
        Assert.False(await featureManager.IsEnabledAsync("enable_beta_banner"));
    }

    [Fact]
    public async Task ProviderReload_AfterChildProviderDisposed_StillPropagatesToFeatureManager()
    {
        // Reproduces the prod sequence: AddPlugins (ServiceCollectionPluginExtensions) builds a
        // temporary ServiceProvider to eagerly construct health-check plugins, resolves their
        // dependencies (which include IConfiguration), then disposes it via `using`. If the host
        // registered the ConfigurationManager via a factory, that disposal disposes the manager —
        // severing the provider->root change-token bridge while leaving direct reads working.
        var (_, client) = MakeToggleClient();
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        ((IConfigurationBuilder)builder.Configuration).Add(new AppConfigAgentConfigurationSource
        {
            HttpClient = client,
            Profile = FlagProfile()
        });

        // Simulate AddPlugins' eager health-check construction: a child provider that resolves
        // IConfiguration and is then disposed.
        using (var tempProvider = builder.Services.BuildServiceProvider())
        {
            _ = tempProvider.GetService<IConfiguration>();
        }

        builder.Services.AddFeatureManagement();

        var app = builder.Build();
        var featureManager = app.Services.GetRequiredService<IFeatureManager>();

        Assert.True(await featureManager.IsEnabledAsync("enable_beta_banner"));

        // Drive the reload through the background service's reload pass (the fix path): after a real
        // change it re-raises the configuration root's change token, so the disposed manager still
        // notifies consumers.
        var reloadService = new AppConfigAgentReloadService(
            app.Configuration, TimeProvider.System, NullLogger<AppConfigAgentReloadService>.Instance);
        var changed = await reloadService.ReloadOnceAsync();

        Assert.True(changed);
        Assert.False(await featureManager.IsEnabledAsync("enable_beta_banner"));
    }

    [Fact]
    public void ConfigurationRootReload_PreservesRuntimeSetValues()
    {
        // The fix calls IConfigurationRoot.Reload(), which re-Loads every provider. Prove a value
        // set at runtime via the indexer (as Program.cs does for ConnectionStrings:DefaultConnection)
        // survives — it lives in the in-memory provider whose Load() is a no-op — so Reload() does
        // not wipe the connection string.
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=runtime;Database=Sebt;";

        var app = builder.Build();
        ((IConfigurationRoot)app.Configuration).Reload();

        Assert.Equal(
            "Server=runtime;Database=Sebt;",
            app.Configuration["ConnectionStrings:DefaultConnection"]);
    }

    [Fact]
    public async Task ProviderReload_AfterHostBuild_FiresConfigRootChangeToken()
    {
        // Arrange
        var (_, client) = MakeToggleClient();
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        ((IConfigurationBuilder)builder.Configuration).Add(new AppConfigAgentConfigurationSource
        {
            HttpClient = client,
            Profile = FlagProfile()
        });

        var app = builder.Build();
        var provider = ((IConfigurationRoot)app.Configuration)
            .Providers
            .OfType<AppConfigAgentConfigurationProvider>()
            .Single();

        var rootFired = false;
        using var registration = ChangeToken.OnChange(
            () => app.Configuration.GetReloadToken(),
            () => rootFired = true);

        // Act
        await provider.ReloadAsync();

        // Assert — a real config change must raise the ROOT token, not just the provider's.
        Assert.True(rootFired);
        Assert.Equal("false", app.Configuration["FeatureManagement:enable_beta_banner"]);
    }
}
