namespace SEBT.Portal.Infrastructure.Data.Entities;

public class DeidentifiedChildResultEntity
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int BirthYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? EligibilityType { get; set; }
    public string? SchoolName { get; set; }
    public EnrollmentCheckSubmissionEntity Submission { get; set; } = null!;
}
