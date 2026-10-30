using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

[Collection("PluginCache")]
public class PluginCacheTests : IDisposable
{
    public PluginCacheTests() => PluginCache.ResetForTesting();
    public void Dispose() => PluginCache.ResetForTesting();

    private static IServiceProvider BuildHostProvider(List<(LogLevel Level, string Message)>? logs = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (logs is not null)
            {
                builder.AddProvider(new CollectingLoggerProvider(logs));
            }
        });
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
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            [CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "true",
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

    [Fact]
    public void GetOrBuild_rebuilds_when_UseMockResponses_changes()
    {
        var host = BuildHostProvider();
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            [CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "true",
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "60",
        });

        var mock = PluginCache.GetOrBuild(host, config);
        config[CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "false";
        config["Cbms:ClientId"] = "id";
        config["Cbms:ClientSecret"] = "secret";
        var live = PluginCache.GetOrBuild(host, config);

        Assert.NotSame(mock, live);
    }

    [Fact]
    public void GetOrBuild_logs_when_the_cache_is_rebuilt()
    {
        var logs = new List<(LogLevel Level, string Message)>();
        var host = BuildHostProvider(logs);
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            [CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "true",
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "60",
        });

        PluginCache.GetOrBuild(host, config);
        PluginCache.GetOrBuild(host, config);

        var rebuilds = RebuildLogs(logs);
        Assert.Single(rebuilds);
        Assert.Contains("True", rebuilds[0].Message, StringComparison.Ordinal);

        config[CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "false";
        config["Cbms:ClientId"] = "id";
        config["Cbms:ClientSecret"] = "secret";
        PluginCache.GetOrBuild(host, config);

        rebuilds = RebuildLogs(logs);
        Assert.Equal(2, rebuilds.Count);
        Assert.Contains("False", rebuilds[1].Message, StringComparison.Ordinal);
    }

    private static List<(LogLevel Level, string Message)> RebuildLogs(List<(LogLevel Level, string Message)> logs) =>
        logs.Where(entry =>
                entry.Level == LogLevel.Information
                && entry.Message.Contains("CBMS household cache rebuilt", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void OverrideForTesting_does_not_rebuild_when_UseMockResponses_changes()
    {
        var fake = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fake);

        var host = BuildHostProvider();
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            [CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "true",
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "60",
        });

        Assert.Same(fake, PluginCache.GetOrBuild(host, config));
        config[CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "false";
        Assert.Same(fake, PluginCache.GetOrBuild(host, config));
    }

    private sealed class CollectingLoggerProvider(List<(LogLevel Level, string Message)> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(entries);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
