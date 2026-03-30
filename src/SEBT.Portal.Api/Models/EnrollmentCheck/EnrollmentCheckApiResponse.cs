namespace SEBT.Portal.Api.Models.EnrollmentCheck;

/// <summary>
/// Response model for enrollment check results.
/// </summary>
public class EnrollmentCheckApiResponse
{
    /// <summary>
    /// Results for each child checked.
    /// </summary>
    public IList<ChildCheckApiResponse> Results { get; init; } = new List<ChildCheckApiResponse>();

    /// <summary>
    /// Optional message from the enrollment check service.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Individual child enrollment check result.
/// </summary>
public class ChildCheckApiResponse
{
    /// <summary>
    /// Unique identifier for this check result.
    /// </summary>
    public string CheckId { get; init; } = string.Empty;

    /// <summary>
    /// Child's first name.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Child's last name.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Child's date of birth in yyyy-MM-dd format.
    /// </summary>
    public string DateOfBirth { get; init; } = string.Empty;

    /// <summary>
    /// Enrollment status (Match, PossibleMatch, NonMatch, Error).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Confidence score for the match, if applicable.
    /// </summary>
    public double? MatchConfidence { get; init; }

    /// <summary>
    /// Type of eligibility (Snap, Tanf, Frp, DirectCert), if matched.
    /// </summary>
    public string? EligibilityType { get; init; }

    /// <summary>
    /// Name of the school associated with the enrollment record.
    /// </summary>
    public string? SchoolName { get; init; }

    /// <summary>
    /// Human-readable status message with additional details.
    /// </summary>
    public string? StatusMessage { get; init; }
}
