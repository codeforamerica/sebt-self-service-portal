using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Infrastructure.Configuration;

public sealed class AppConfigAgentReloadService : BackgroundService
{
    private const int DefaultReloadAfterSeconds = 90;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromHours(1);

    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AppConfigAgentReloadService> _logger;

    public AppConfigAgentReloadService(
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<AppConfigAgentReloadService> logger)
    {
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = ResolveIntervalSeconds();
        if (intervalSeconds <= 0)
        {
            _logger.LogInformation(
                "AppConfig reload polling is disabled (ReloadAfterSeconds = {IntervalSeconds})",
                intervalSeconds);
            return;
        }

        // The DI-registered IConfiguration is the configuration root; its providers include any
        // AppConfig agent providers added at startup. Holding this list also keeps the providers
        // rooted for the app's lifetime.
        var providers = (_configuration as IConfigurationRoot)?
            .Providers
            .OfType<AppConfigAgentConfigurationProvider>()
            .ToList();

        if (providers is null || providers.Count == 0)
        {
            _logger.LogInformation("No AppConfig agent providers found; reload polling will not run.");
            return;
        }

        // Create the timer before logging "started": the test waits for that log message and
        // immediately advances FakeTimeProvider. PeriodicTimer buffers one tick once it exists,
        // so the advance is safe even if WaitForNextTickAsync hasn't been called yet. Logging
        // before timer creation loses the tick when the test's Advance() races the constructor.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds), _timeProvider);
        var lastHeartbeat = _timeProvider.GetUtcNow();

        _logger.LogInformation(
            "AppConfig reload polling started: interval = {IntervalSeconds}s, providers = {Providers}.",
            intervalSeconds,
            string.Join(", ", providers));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ReloadOnceAsync(stoppingToken).ConfigureAwait(false);

                var now = _timeProvider.GetUtcNow();
                if (now - lastHeartbeat >= HeartbeatInterval)
                {
                    lastHeartbeat = now;
                    _logger.LogInformation(
                        "AppConfig reload polling alive. Interval = {IntervalSeconds}.",
                        intervalSeconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown when the stopping token is canceled.
        }
    }

    /// <summary>
    /// Performs a single reload pass: re-fetches every AppConfig agent provider and, when any of
    /// them changed, re-raises the configuration root's change token so cached consumers
    /// (<c>IFeatureManager</c>, <c>IOptionsMonitor&lt;T&gt;</c>) refresh.
    /// </summary>
    /// <returns><c>true</c> if any provider's configuration changed; otherwise <c>false</c>.</returns>
    internal async Task<bool> ReloadOnceAsync(CancellationToken cancellationToken = default)
    {
        var providers = (_configuration as IConfigurationRoot)?
            .Providers
            .OfType<AppConfigAgentConfigurationProvider>()
            .ToList();

        if (providers is null || providers.Count == 0)
        {
            return false;
        }

        var anyChanged = false;
        foreach (var provider in providers)
        {
            try
            {
                anyChanged |= await provider.ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // One failing reload must not stop the loop, the next tick retries.
                _logger.LogWarning(ex,
                    "AppConfig reload failed for {Provider}, will retry at the next interval.",
                    provider);
            }
        }

        if (anyChanged)
        {
            try
            {
                // The host's ConfigurationManager is disposed during startup: a transient
                // ServiceProvider built to construct plugin health checks takes ownership of it and
                // disposes it, severing the provider -> configuration-root change-token bridge. After
                // that, a provider's OnReload() no longer reaches IFeatureManager / IOptionsMonitor<T>.
                // IConfigurationRoot.Reload() calls RaiseChanged() directly, so consumers are
                // re-notified even on a disposed manager. The providers' own Load() short-circuits when
                // disposed, so this does not re-fetch from the agent.
                (_configuration as IConfigurationRoot)?.Reload();
            }
            catch (Exception ex)
            {
                // Reload() runs change-token consumers inline. IOptionsMonitor is one: it evicts its
                // cache and rebuilds the options, running every IValidateOptions. A validator that
                // rejects the new configuration throws from here, and nothing between us and it
                // catches. Unhandled, that faults this BackgroundService and the host's default
                // StopHost behavior terminates the process, which a restart cannot fix while the bad
                // value is still published. Absorb it and keep polling instead. The rejected section's
                // options cache is already evicted, so its consumers fail until the configuration is
                // corrected, at which point the next reload rebuilds it and they recover on their own.
                _logger.LogCritical(ex,
                    "AppConfig change rejected while notifying configuration consumers. Consumers of "
                    + "the rejected section will fail until the configuration is corrected.");
            }
        }

        return anyChanged;
    }

    private int ResolveIntervalSeconds()
    {
        return int.TryParse(_configuration["AppConfig:Agent:ReloadAfterSeconds"], out var seconds)
            ? seconds
            : DefaultReloadAfterSeconds;
    }
}
