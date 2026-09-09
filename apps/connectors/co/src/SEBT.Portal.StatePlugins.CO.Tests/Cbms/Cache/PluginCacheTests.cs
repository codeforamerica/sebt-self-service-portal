using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

[Collection("PluginCache")]
public class PluginCacheTests : IDisposable
{
    public PluginCacheTests() => PluginCache.ResetForTesting();
    public void Dispose() => PluginCache.ResetForTesting();

    private static IServiceProvider BuildHostProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<IHostApplicationLifetime>(_ => Substitute.For<IHostApplicationLifetime>());
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns(c => "h:" + c.Arg<string>());
        services.AddSingleton(hasher);
        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cbms:UseMockResponses"] = "true",
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "60",
        })
        .Build();

    [Fact]
    public void GetOrBuild_returns_singleton_across_calls()
    {
        var host = BuildHostProvider();
        var config = BuildConfig();

        var a = PluginCache.GetOrBuild(host, config);
        var b = PluginCache.GetOrBuild(host, config);

        Assert.Same(a, b);
    }

    [Fact]
    public void OverrideForTesting_replaces_instance()
    {
        var fake = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fake);

        var host = BuildHostProvider();
        var resolved = PluginCache.GetOrBuild(host, BuildConfig());

        Assert.Same(fake, resolved);
    }

    [Fact]
    public void ResetForTesting_clears_instance()
    {
        var host = BuildHostProvider();
        var config = BuildConfig();

        var a = PluginCache.GetOrBuild(host, config);
        PluginCache.ResetForTesting();
        var b = PluginCache.GetOrBuild(host, config);

        Assert.NotSame(a, b);
    }
}
