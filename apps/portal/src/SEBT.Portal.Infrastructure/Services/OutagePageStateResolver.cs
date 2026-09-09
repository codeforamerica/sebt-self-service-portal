using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Default <see cref="IOutagePageStateResolver"/>: schedule wins per-surface when windows
/// targeting that surface exist; otherwise the surface's manual feature flag decides.
/// </summary>
public sealed class OutagePageStateResolver : IOutagePageStateResolver
{
    private readonly IOutageScheduleEvaluator _scheduleEvaluator;
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<OutagePageStateResolver> _logger;

    public OutagePageStateResolver(
        IOutageScheduleEvaluator scheduleEvaluator,
        IFeatureManager featureManager,
        ILogger<OutagePageStateResolver> logger)
    {
        _scheduleEvaluator = scheduleEvaluator;
        _featureManager = featureManager;
        _logger = logger;
    }

    public async Task<OutagePageState> ResolveAsync(OutageTarget surface)
    {
        var manualFlagName = surface switch
        {
            OutageTarget.Portal => FeatureFlags.OutagePageEnabled,
            OutageTarget.EnrollmentChecker => FeatureFlags.CheckerOutagePageEnabled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(surface),
                surface,
                "Pass a single surface (Portal or EnrollmentChecker); Both is a window-level value.")
        };

        if (_scheduleEvaluator.HasScheduledWindows(surface))
        {
            var scheduleActive = _scheduleEvaluator.IsOutageActive(surface);
            // Info only when active: this resolves on every features poll, and the steady
            // "no outage" case would otherwise flood logs; Debug keeps it traceable.
            if (scheduleActive)
            {
                _logger.LogInformation(
                    "Outage page active for {Surface}: schedule is authority and a window is active",
                    surface);
            }
            else
            {
                _logger.LogDebug(
                    "Outage page inactive for {Surface}: schedule is authority, no active window",
                    surface);
            }

            return new OutagePageState(scheduleActive, ScheduleIsAuthority: true);
        }

        var manualFlag = await _featureManager.IsEnabledAsync(manualFlagName);
        if (manualFlag)
        {
            _logger.LogInformation(
                "Outage page active for {Surface}: no scheduled windows, manual flag {FlagName} is on",
                surface,
                manualFlagName);
        }
        else
        {
            _logger.LogDebug(
                "Outage page inactive for {Surface}: no scheduled windows, manual flag {FlagName} is off",
                surface,
                manualFlagName);
        }

        return new OutagePageState(manualFlag, ScheduleIsAuthority: false);
    }
}
