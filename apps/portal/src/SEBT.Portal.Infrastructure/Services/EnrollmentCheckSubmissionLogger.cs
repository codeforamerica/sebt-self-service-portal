using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Services;

public class EnrollmentCheckSubmissionLogger(PortalDbContext dbContext) : IEnrollmentCheckSubmissionLogger
{
    public async Task LogSubmissionAsync(
        EnrollmentCheckSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var entity = new EnrollmentCheckSubmissionEntity
        {
            Id = submission.SubmissionId,
            CheckedAtUtc = submission.CheckedAtUtc,
            ChildrenChecked = submission.ChildrenChecked,
            IpAddressHash = submission.IpAddressHash,
            ChildResults = submission.ChildResults.Select(cr => new DeidentifiedChildResultEntity
            {
                Id = Guid.NewGuid(),
                BirthYear = cr.BirthYear,
                Status = cr.Status,
                EligibilityType = cr.EligibilityType,
                SchoolName = cr.SchoolName
            }).ToList()
        };

        dbContext.EnrollmentCheckSubmissions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
