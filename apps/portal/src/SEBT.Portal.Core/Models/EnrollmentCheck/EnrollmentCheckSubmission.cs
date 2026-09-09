namespace SEBT.Portal.Core.Models.EnrollmentCheck;

public class EnrollmentCheckSubmission
{
    public Guid SubmissionId { get; init; }
    public DateTime CheckedAtUtc { get; init; }
    public int ChildrenChecked { get; init; }
    public string? IpAddressHash { get; init; }
    public IList<DeidentifiedChildResult> ChildResults { get; init; } = new List<DeidentifiedChildResult>();
}
