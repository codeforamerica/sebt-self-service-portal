namespace SEBT.Portal.Infrastructure.Data.Entities;

public class EnrollmentCheckSubmissionEntity
{
    public Guid Id { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public int ChildrenChecked { get; set; }
    public string? IpAddressHash { get; set; }
    public ICollection<DeidentifiedChildResultEntity> ChildResults { get; set; } = new List<DeidentifiedChildResultEntity>();
}
