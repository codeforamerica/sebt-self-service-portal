extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// API response model for a child on a benefit application.
/// </summary>
public record ChildResponse
{
    /// <summary>
    /// The child's first name.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// The application status for this child.
    /// </summary>
    public Core::SEBT.Portal.Core.Models.Household.ApplicationStatus Status { get; init; }
}
