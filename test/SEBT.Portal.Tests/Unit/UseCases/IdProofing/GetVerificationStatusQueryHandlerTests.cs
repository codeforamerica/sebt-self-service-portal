using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.TestUtilities.Helpers;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class GetVerificationStatusQueryHandlerTests
{
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly NullLogger<GetVerificationStatusQueryHandler> logger =
        NullLogger<GetVerificationStatusQueryHandler>.Instance;

    private GetVerificationStatusQueryHandler CreateHandler() =>
        new(challengeRepository, logger);

    // --- Not found ---

    [Fact]
    public async Task Handle_ShouldReturn404_WhenChallengeNotFound()
    {
        var handler = CreateHandler();
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<VerificationStatusResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, preconditionFailed.Reason);
    }

    // --- Pending status ---

    [Fact]
    public async Task Handle_ShouldReturnPending_WhenChallengeIsPending()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30); // Not expired
        });
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pending", result.Value.Status);
    }

    // --- Verified status ---

    [Fact]
    public async Task Handle_ShouldReturnVerified_WhenChallengeIsVerified()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateVerifiedChallenge();
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("verified", result.Value.Status);
    }

    // --- Rejected status ---

    [Fact]
    public async Task Handle_ShouldReturnRejected_WhenChallengeIsRejected()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateRejectedChallenge("docVerificationFailed");
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("rejected", result.Value.Status);
        Assert.Equal("docVerificationFailed", result.Value.OffboardingReason);
    }

    // --- Check-on-read expiration (Codex test 7) ---

    [Fact]
    public async Task Handle_ShouldExpireChallenge_WhenPendingAndPastExpiresAt()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(-5); // Already expired
        });
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("rejected", result.Value.Status);

        // Should persist the expiration
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Expired), Arg.Any<CancellationToken>());
    }

    // --- Check-on-read expiration for Created challenges (F11) ---

    [Fact]
    public async Task Handle_ShouldExpireChallenge_WhenCreatedAndPastExpiresAt()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallenge(c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(-10); // Created but expired
        });
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("rejected", result.Value.Status);

        // Should persist the expiration
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Expired), Arg.Any<CancellationToken>());
    }

    // --- AllowIdRetry is included in response (D9) ---

    [Fact]
    public async Task Handle_ShouldIncludeAllowIdRetry_InResponse()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.AllowIdRetry = true;
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(query.ChallengeId, query.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AllowIdRetry);
    }
}
