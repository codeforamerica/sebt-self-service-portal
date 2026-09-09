using SEBT.Portal.Api.Composition.Defaults;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class DefaultCardReplacementServiceTests
{
    [Fact]
    public async Task RequestCardReplacementAsync_ReturnsNotConfiguredBackendError()
    {
        var service = new DefaultCardReplacementService();
        var request = new CardReplacementRequest
        {
            HouseholdIdentifierValue = "guardian@example.com",
            CaseRefs = [new CaseRef { SummerEbtCaseId = "SEBT-001" }],
            Reason = CardReplacementReason.Unspecified
        };

        var result = await service.RequestCardReplacementAsync(request);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
        Assert.Equal("No card replacement service configured.", result.ErrorMessage);
    }
}
