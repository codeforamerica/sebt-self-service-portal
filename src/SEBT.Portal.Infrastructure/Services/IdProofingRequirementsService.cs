using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Determines which PII data elements a user can view based on their ID proofing status
/// and the state-specific configuration.
/// IdProofingStatus.Completed is treated as meeting IAL1+ requirements
/// </summary>
public class IdProofingRequirementsService : IIdProofingRequirementsService
{
    private readonly IdProofingRequirementsSettings _settings;

    public IdProofingRequirementsService(IOptions<IdProofingRequirementsSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public PiiVisibility GetPiiVisibility(IdProofingStatus idProofingStatus)
    {
        var meetsIal1 = idProofingStatus == IdProofingStatus.Completed;

        return new PiiVisibility(
            IncludeAddress: MeetsRequirement(_settings.Address, meetsIal1),
            IncludeEmail: MeetsRequirement(_settings.Email, meetsIal1),
            IncludePhone: MeetsRequirement(_settings.Phone, meetsIal1));
    }

    private static bool MeetsRequirement(string requirement, bool meetsIal1)
    {
        if (string.IsNullOrWhiteSpace(requirement) ||
            requirement.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (requirement.Equals("IAL1", StringComparison.OrdinalIgnoreCase))
        {
            return meetsIal1;
        }

        // We can add more IAL levels here in the future if needed
        return meetsIal1;
    }
}
