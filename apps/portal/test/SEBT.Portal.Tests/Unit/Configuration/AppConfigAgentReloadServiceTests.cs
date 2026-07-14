using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Infrastructure.Configuration;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class AppConfigAgentReloadServiceTests
{
    // Generous wall-clock budget for an async loop continuation to run after fakeTime.Advance().
    // WaitForAsync returns as soon as the condition is met, so a large timeout only affects the
    // failure path — it keeps the test from flaking under a loaded, parallel test suite.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task ReloadLoop_FiresMoreThanOnce()
    {
        var handler = new CountingHandler();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = BuildConfig(handler, reloadAfterSeconds: 1);
        var fakeTime = new FakeTimeProvider();
        var service = new AppConfigAgentReloadService(config, fakeTime, StartSignalLogger(started));

        await service.StartAsync(CancellationToken.None);
        await EnsureLoopArmed(started);
        var afterStart = handler.Count; // initial load already happened during config build

        // Each advance is one reload interval; the loop should fire on each.
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await WaitForAsync(() => handler.Count >= afterStart + 1, WaitTimeout);

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await WaitForAsync(() => handler.Count >= afterStart + 2, WaitTimeout);

        await service.StopAsync(CancellationToken.None);

        Assert.True(
            handler.Count >= afterStart + 2,
            $"expected at least 2 reloads after start; afterStart={afterStart}, total={handler.Count}");
    }

    [Fact]
    public async Task ReloadLoop_ContinuesAfterAFailedReload()
    {
        // Call 1 = initial load (during config build). Call 2 = first reload, which throws.
        // The loop must survive and still perform the next reload (call 3).
        var handler = new CountingHandler();
        handler.ThrowOnCalls.Add(2);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = BuildConfig(handler, reloadAfterSeconds: 1);
        var fakeTime = new FakeTimeProvider();
        var service = new AppConfigAgentReloadService(config, fakeTime, StartSignalLogger(started));

        await service.StartAsync(CancellationToken.None);
        await EnsureLoopArmed(started);
        var afterStart = handler.Count;

        fakeTime.Advance(TimeSpan.FromSeconds(1)); // failing reload
        await WaitForAsync(() => handler.Count >= afterStart + 1, WaitTimeout);

        fakeTime.Advance(TimeSpan.FromSeconds(1)); // must still happen despite the prior failure
        await WaitForAsync(() => handler.Count >= afterStart + 2, WaitTimeout);

        await service.StopAsync(CancellationToken.None);

        Assert.True(
            handler.Count >= afterStart + 2,
            "loop should keep polling after a failed reload");
    }

    [Fact]
    public async Task ReloadOnce_WhenAConfigurationConsumerThrows_SwallowsItAndLogsCritical()
    {
        // IConfigurationRoot.Reload() invokes change-token consumers inline, and IOptionsMonitor
        // is one of them: it evicts its cache and immediately rebuilds the options, running every
        // registered IValidateOptions. A validator that rejects the newly fetched configuration
        // therefore throws from inside Reload(). ChangeToken does not catch consumer exceptions,
        // so unhandled it faults this BackgroundService, and the default StopHost behavior
        // terminates the process. Reload polling must instead stay alive and shout.
        var handler = new CountingHandler { ContentVariesPerCall = true };
        var config = BuildConfig(handler, reloadAfterSeconds: 1);
        var entries = new List<(LogLevel Level, string Message)>();
        var service = new AppConfigAgentReloadService(
            config,
            new FakeTimeProvider(),
            new CallbackLogger((level, message) => entries.Add((level, message))));

        // A plain ConfigurationRoot keeps the provider -> root change-token bridge intact, so the
        // provider's own OnReload() already notified consumers before ReloadOnceAsync reaches
        // Reload(). In the running app that bridge is severed (see ReloadOnceAsync), leaving
        // Reload() as the only notifier. Ignoring the first notification models the deployed path.
        var notifications = 0;
        using var registration = ChangeToken.OnChange(
            config.GetReloadToken,
            () =>
            {
                if (Interlocked.Increment(ref notifications) > 1)
                {
                    throw new OptionsValidationException(
                        "TestSettings",
                        typeof(object),
                        ["Simulated validation failure for a rejected configuration value."]);
                }
            });

        var exception = await Record.ExceptionAsync(() => service.ReloadOnceAsync());

        Assert.Null(exception);
        Assert.Contains(entries, entry => entry.Level == LogLevel.Critical);
    }

    private static IConfigurationRoot BuildConfig(HttpMessageHandler handler, int reloadAfterSeconds)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2772") };
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppConfig:Agent:ReloadAfterSeconds"] = reloadAfterSeconds.ToString(),
        });
        ((IConfigurationBuilder)builder).Add(new AppConfigAgentConfigurationSource
        {
            HttpClient = httpClient,
            Profile = new AppConfigAgentProfile
            {
                BaseUrl = "http://localhost:2772",
                ApplicationId = "test-app",
                EnvironmentId = "test-env",
                ProfileId = "test-profile",
                IsFeatureFlag = false,
            },
            Logger = NullLogger<AppConfigAgentConfigurationProvider>.Instance,
        });
        return builder.Build();
    }

    // BackgroundService.StartAsync returns before ExecuteAsync has armed its PeriodicTimer,
    // so advancing fake time too early loses the first tick. The service logs "polling started"
    // immediately before arming the timer; waiting for it removes the race deterministically.
    private static ILogger<AppConfigAgentReloadService> StartSignalLogger(TaskCompletionSource started)
    {
        return new CallbackLogger((_, message) =>
        {
            if (message.Contains("reload polling started", StringComparison.OrdinalIgnoreCase))
            {
                started.TrySetResult();
            }
        });
    }

    private static async Task EnsureLoopArmed(TaskCompletionSource started)
    {
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // Condition-based wait: polls instead of sleeping a fixed duration, so the test
    // is robust to scheduling of the async loop body after fakeTime.Advance().
    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _count;
        public int Count => _count;
        public HashSet<int> ThrowOnCalls { get; } = new();

        /// <summary>
        /// Returns a different payload on every call, so each provider load reports a change.
        /// Off by default: the other tests rely on a stable payload so no reload propagates.
        /// </summary>
        public bool ContentVariesPerCall { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _count);
            if (ThrowOnCalls.Contains(call))
            {
                throw new HttpRequestException("Simulated agent failure");
            }

            var value = ContentVariesPerCall ? $"value{call}" : "value1";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"Key1":"{{value}}"}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CallbackLogger : ILogger<AppConfigAgentReloadService>
    {
        private readonly Action<LogLevel, string> _onLog;

        public CallbackLogger(Action<LogLevel, string> onLog)
        {
            _onLog = onLog;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _onLog(logLevel, formatter(state, exception));
        }
    }
}
