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
    private readonly IdProofingRequirementsSettings _settings;
    private readonly ILogger<IdProofingRequirementsService> _logger;

    public IdProofingRequirementsService(
        IOptions<IdProofingRequirementsSettings> settings,
        ILogger<IdProofingRequirementsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public PiiVisibility GetPiiVisibility(IdProofingStatus idProofingStatus)
    {
        var meetsIal1 = idProofingStatus == IdProofingStatus.Completed;

        return new PiiVisibility(
            IncludeAddress: MeetsRequirement("Address", _settings.Address, meetsIal1),
            IncludeEmail: MeetsRequirement("Email", _settings.Email, meetsIal1),
            IncludePhone: MeetsRequirement("Phone", _settings.Phone, meetsIal1));
    }

    private bool MeetsRequirement(string fieldName, string requirement, bool meetsIal1)
    {
        if (string.IsNullOrWhiteSpace(requirement))
        {
            return false;
        }

        if (requirement.Equals("IAL1", StringComparison.OrdinalIgnoreCase))
        {
            return meetsIal1;
        }

        if (requirement.Equals("IAL1plus", StringComparison.OrdinalIgnoreCase))
        {
            return meetsIal1; // IdProofingStatus.Completed is treated as IAL1+
        }

        if (requirement.Equals("IAL2", StringComparison.OrdinalIgnoreCase))
        {
            return false; // IAL2 not yet supported; fail-safe
        }

        _logger.LogWarning(
            "Unknown IdProofing requirement value '{Requirement}' for {FieldName}. Defaulting to fail-safe (PII hidden). Valid values: IAL1, IAL1plus, IAL2.",
            requirement,
            fieldName);
        return false;
    }
}
