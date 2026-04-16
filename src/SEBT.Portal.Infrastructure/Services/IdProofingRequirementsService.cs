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
/// <remarks>
/// This service is superseded by the unified IAL requirements system and will be deleted
/// in a forthcoming task. See <c>IdProofingRequirementsSettings</c> and
/// <c>ConfigureIdProofingRequirements</c> for the replacement.
/// </remarks>
public class IdProofingRequirementsService : IIdProofingRequirementsService
{
    public IdProofingRequirementsService(
        IOptionsSnapshot<IdProofingRequirementsSettings> settingsSnapshot)
    {
    }

    /// <inheritdoc />
    public PiiVisibility GetPiiVisibility(UserIalLevel userIalLevel)
    {
        throw new NotImplementedException(
            "IdProofingRequirementsService has been superseded. " +
            "Use the unified IdProofingRequirementsSettings with ConfigureIdProofingRequirements.");
    }
}
