using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class EnrollmentCheckSubmissionLoggerTests
{
    private static PortalDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PortalDbContext(options);
    }

    [Fact]
    public async Task LogSubmissionAsync_PersistsDeidentifiedData()
    {
        using var context = CreateInMemoryContext();
        var logger = new EnrollmentCheckSubmissionLogger(context);

        var submission = new EnrollmentCheckSubmission
        {
            SubmissionId = Guid.NewGuid(),
            CheckedAtUtc = DateTime.UtcNow,
            ChildrenChecked = 1,
            IpAddressHash = "abc123hash",
            ChildResults = new List<DeidentifiedChildResult>
            {
                new()
                {
                    BirthYear = 2015,
                    Status = "Match",
                    EligibilityType = "SNAP",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await logger.LogSubmissionAsync(submission);

        var stored = await context.EnrollmentCheckSubmissions
            .Include(s => s.ChildResults)
            .FirstOrDefaultAsync();

        Assert.NotNull(stored);
        Assert.Equal(1, stored!.ChildrenChecked);
        Assert.Equal("abc123hash", stored.IpAddressHash);
        Assert.Single(stored.ChildResults);
        Assert.Equal(2015, stored.ChildResults.First().BirthYear);
        Assert.Equal("Match", stored.ChildResults.First().Status);
    }
}
