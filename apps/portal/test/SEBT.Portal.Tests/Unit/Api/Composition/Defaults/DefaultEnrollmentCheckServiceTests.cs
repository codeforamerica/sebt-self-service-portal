using SEBT.Portal.Api.Composition.Defaults;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class DefaultEnrollmentCheckServiceTests
{
    [Fact]
    public async Task CheckEnrollmentAsync_ReturnsNonMatchPerChild_EchoingSubmittedFields()
    {
        var service = new DefaultEnrollmentCheckService();
        var firstCheckId = Guid.NewGuid();
        var secondCheckId = Guid.NewGuid();
        var request = new EnrollmentCheckRequest
        {
            Children =
            [
                new ChildCheckRequest
                {
                    CheckId = firstCheckId,
                    FirstName = "Avery",
                    LastName = "Testchild",
                    DateOfBirth = new DateOnly(2016, 4, 12),
                    SchoolName = "Test Elementary"
                },
                new ChildCheckRequest
                {
                    CheckId = secondCheckId,
                    FirstName = "Blake",
                    LastName = "Testchild",
                    DateOfBirth = new DateOnly(2018, 9, 3)
                }
            ]
        };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.Equal("No enrollment check service configured.", result.ResponseMessage);
        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.Equal(EnrollmentStatus.NonMatch, r.Status));
        var first = result.Results[0];
        Assert.Equal(firstCheckId, first.CheckId);
        Assert.Equal("Avery", first.FirstName);
        Assert.Equal("Testchild", first.LastName);
        Assert.Equal(new DateOnly(2016, 4, 12), first.DateOfBirth);
        Assert.Equal("Test Elementary", first.SchoolName);
        Assert.Equal(secondCheckId, result.Results[1].CheckId);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_ReturnsEmptyResults_ForNoChildren()
    {
        var service = new DefaultEnrollmentCheckService();
        var request = new EnrollmentCheckRequest { Children = [] };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.Empty(result.Results);
    }
}
