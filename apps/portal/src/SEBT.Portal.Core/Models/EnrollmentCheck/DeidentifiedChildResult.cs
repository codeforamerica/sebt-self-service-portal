namespace SEBT.Portal.Core.Models.EnrollmentCheck;

public class DeidentifiedChildResult
{
    public int BirthYear { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? EligibilityType { get; init; }
    public string? SchoolName { get; init; }
}
