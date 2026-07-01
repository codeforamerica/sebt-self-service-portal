using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class StubSocureClientTests
{
    private readonly StubSocureClient _client = new(NullLogger<StubSocureClient>.Instance);

    [Fact]
    public async Task RunIdProofingAssessmentAsync_ReturnsEgregiousReasonCodes_ForEgregiousPersonaSsn()
    {
        var result = await _client.RunIdProofingAssessmentAsync(
            Guid.NewGuid(),
            "test@example.com",
            "1990-01-01",
            "ssn",
            "999-99-7815");

        Assert.True(result.IsSuccess);
        Assert.Equal(IdProofingOutcome.DocumentVerificationRequired, result.Value.Outcome);
        Assert.Equal(["R815"], result.Value.DocumentVerificationReasonCodes);
    }

    [Fact]
    public async Task RunIdProofingAssessmentAsync_DoesNotReturnEgregiousReasonCodes_ForOrdinarySsn()
    {
        var result = await _client.RunIdProofingAssessmentAsync(
            Guid.NewGuid(),
            "test@example.com",
            "1990-01-01",
            "ssn",
            "123456789");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DocumentVerificationReasonCodes);
    }
}
