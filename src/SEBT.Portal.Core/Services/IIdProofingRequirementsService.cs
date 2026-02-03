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

/// <summary>
/// Indicates which PII data elements a user is allowed to view.
/// </summary>
/// <param name="IncludeAddress">Whether the user can view address information.</param>
/// <param name="IncludeEmail">Whether the user can view email information.</param>
/// <param name="IncludePhone">Whether the user can view phone information.</param>
public record PiiVisibility(bool IncludeAddress, bool IncludeEmail, bool IncludePhone);
