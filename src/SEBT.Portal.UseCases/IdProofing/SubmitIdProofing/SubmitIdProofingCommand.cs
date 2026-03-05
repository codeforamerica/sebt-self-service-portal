using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Command to submit ID proofing data for risk assessment.
/// Dispatched when the user submits their DOB and optional government ID.
/// </summary>
public class SubmitIdProofingCommand : ICommand<SubmitIdProofingResponse>
{
    /// <summary>
    /// The authenticated user's internal ID.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer.")]
    public int UserId { get; init; }

    /// <summary>
    /// User's date of birth in yyyy-MM-dd format.
    /// </summary>
    [Required(ErrorMessage = "DateOfBirth is required.")]
    public string DateOfBirth { get; init; } = string.Empty;

    /// <summary>
    /// Type of government ID provided (e.g., "ssn", "itin"), or null if the user opted out.
    /// A null idType triggers the noIdProvided off-boarding path.
    /// </summary>
    public string? IdType { get; init; }

    /// <summary>
    /// The government ID value, or null if the user opted out.
    /// </summary>
    public string? IdValue { get; init; }
}
