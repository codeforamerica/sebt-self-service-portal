using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Determines which PII data elements a user can view based on their ID proofing status
/// and the state-specific configuration.
/// </summary>
public interface IIdProofingRequirementsService
{
    /// <summary>
    /// Returns which PII elements the user is allowed to view based on their ID proofing status
    /// and the configured state requirements.
    /// </summary>
    /// <param name="idProofingStatus">The user's current ID proofing status from their JWT.</param>
    /// <returns>Flags indicating which PII types the user can view.</returns>
    PiiVisibility GetPiiVisibility(IdProofingStatus idProofingStatus);
}
