using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        return new CallbackLogger(message =>
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _count);
            if (ThrowOnCalls.Contains(call))
            {
                throw new HttpRequestException("Simulated agent failure");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"Key1":"value1"}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CallbackLogger : ILogger<AppConfigAgentReloadService>
    {
        private readonly Action<string> _onLog;

        public CallbackLogger(Action<string> onLog)
        {
            _onLog = onLog;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _onLog(formatter(state, exception));
        }
    }
}
