using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Determines which PII data elements a user can view based on their ID proofing status
/// and the state-specific configuration.
/// </summary>
public class IdProofingRequirementsService : IIdProofingRequirementsService
{
    private readonly IOptionsMonitor<IdProofingRequirementsSettings> _optionsMonitor;
    private readonly ILogger<IdProofingRequirementsService> _logger;

    public IdProofingRequirementsService(
        IOptionsMonitor<IdProofingRequirementsSettings> optionsMonitor,
        ILogger<IdProofingRequirementsService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <inheritdoc />
    public PiiVisibility GetPiiVisibility(UserIalLevel userIalLevel)
    {
        var settings = _optionsMonitor.CurrentValue;
        return new PiiVisibility(
            IncludeAddress: MeetsRequirement("Address", settings.AddressView, userIalLevel),
            IncludeEmail: MeetsRequirement("Email", settings.EmailView, userIalLevel),
            IncludePhone: MeetsRequirement("Phone", settings.PhoneView, userIalLevel));
    }

    private bool MeetsRequirement(string fieldName, IalLevel requirement, UserIalLevel userIalLevel)
    {
        return requirement switch
        {
            IalLevel.IAL1 => userIalLevel is UserIalLevel.IAL1 or UserIalLevel.IAL1plus or UserIalLevel.IAL2,
            IalLevel.IAL1plus => userIalLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2,
            IalLevel.IAL2 => userIalLevel == UserIalLevel.IAL2, // IAL2 not yet supported for users; fail-safe
            _ => FailSafeUnknown(fieldName, requirement)
        };
    }

    private bool FailSafeUnknown(string fieldName, IalLevel requirement)
    {
        _logger.LogWarning(
            "Unknown IdProofing requirement value '{Requirement}' for {FieldName}. Defaulting to fail-safe (PII hidden). Valid values: IAL1, IAL1plus, IAL2.",
            requirement,
            fieldName);
        return false;
    }
}
