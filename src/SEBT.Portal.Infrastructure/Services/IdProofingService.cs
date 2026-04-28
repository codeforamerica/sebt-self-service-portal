using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Unified identity proofing service. Evaluates IAL requirements for
/// resource+action pairs and determines PII visibility.
/// Singleton lifetime — holds a volatile reference to settings that is
/// swapped safely on config changes.
/// </summary>
public class IdProofingService : IIdProofingService, IPiiVisibilityService
{
    // volatile ensures that when the OnChange callback swaps this reference,
    // HTTP request threads on other CPU cores see the new value immediately
    // rather than reading a stale cached copy from their local CPU cache.
    private volatile IdProofingRequirementsSettings _settings;
    private readonly ILogger<IdProofingService> _logger;

    public IdProofingService(
        IOptionsMonitor<IdProofingRequirementsSettings> monitor,
        ILogger<IdProofingService> logger)
    {
        _settings = monitor.CurrentValue;
        _logger = logger;

        monitor.OnChange(_ =>
        {
            try
            {
                _settings = monitor.CurrentValue;
                logger.LogInformation("IdProofingRequirements config reloaded successfully");
            }
            catch (OptionsValidationException ex)
            {
                logger.LogCritical(
                    ex,
                    "IdProofingRequirements config change rejected — retaining previous valid config. "
                    + "This is a SECURITY configuration failure that must be fixed immediately.");
            }
        });
    }

    public IdProofingDecision Evaluate(
        ProtectedResource resource,
        ProtectedAction action,
        UserIalLevel userIalLevel,
        IReadOnlyList<SummerEbtCase> cases)
    {
        var requirement = _settings.Get(resource, action);
        var requiredLevel = requirement.Resolve(cases);
        return new IdProofingDecision(
            IsAllowed: userIalLevel >= requiredLevel,
            RequiredLevel: requiredLevel);
    }

    public PiiVisibility GetVisibility(UserIalLevel userIalLevel)
    {
        return new PiiVisibility(
            IncludeAddress: EvaluateView(ProtectedResource.Address, userIalLevel),
            IncludeEmail: EvaluateView(ProtectedResource.Email, userIalLevel),
            IncludePhone: EvaluateView(ProtectedResource.Phone, userIalLevel));
    }

    private bool EvaluateView(ProtectedResource resource, UserIalLevel userIalLevel)
    {
        var requirement = _settings.Get(resource, ProtectedAction.View);
        var requiredLevel = requirement.Resolve([]);
        return userIalLevel >= requiredLevel;
    }
}
