using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.EnrollmentCheck;

/// <summary>
/// Request model for checking enrollment status of one or more children.
/// </summary>
public class EnrollmentCheckApiRequest
{
    /// <summary>
    /// Maximum number of children that can be checked in a single request.
    /// </summary>
    public const int MaxChildren = 20;

    /// <summary>
    /// The children to check enrollment for.
    /// </summary>
    [MaxLength(MaxChildren)]
    public IList<ChildCheckApiRequest> Children { get; set; } = new List<ChildCheckApiRequest>();
}

/// <summary>
/// Individual child data for an enrollment check request.
/// </summary>
public class ChildCheckApiRequest
{
    /// <summary>
    /// Child's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Child's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Child's date of birth in yyyy-MM-dd format.
    /// </summary>
    public string DateOfBirth { get; set; } = string.Empty;

    /// <summary>
    /// Name of the child's school (optional).
    /// </summary>
    public string? SchoolName { get; set; }

    /// <summary>
    /// Code identifying the child's school (optional).
    /// </summary>
    public string? SchoolCode { get; set; }

    /// <summary>
    /// State-specific additional fields (optional).
    /// </summary>
    public IDictionary<string, string> AdditionalFields { get; set; } = new Dictionary<string, string>();
}
