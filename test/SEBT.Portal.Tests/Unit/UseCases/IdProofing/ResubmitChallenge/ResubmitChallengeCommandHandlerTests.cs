using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.TestUtilities.Helpers;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing.ResubmitChallenge;

public class ResubmitChallengeCommandHandlerTests
{
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IHouseholdRepository householdRepository = Substitute.For<IHouseholdRepository>();
    private readonly ISocureClient socureClient = Substitute.For<ISocureClient>();
    private readonly SocureSettings socureSettings = new()
    {
        DocvStepupWorkflow = "docv_stepup",
        ChallengeExpirationMinutes = 30
    };
    private readonly IValidator<ResubmitChallengeCommand> validator =
        new DataAnnotationsValidator<ResubmitChallengeCommand>(null!);
    private readonly NullLogger<ResubmitChallengeCommandHandler> logger =
        NullLogger<ResubmitChallengeCommandHandler>.Instance;

    private ResubmitChallengeCommandHandler CreateHandler() => new(
        challengeRepository, userRepository, householdRepository,
        socureClient, socureSettings, validator, logger);

    private static SocureDocvSession FreshSession() => new(
        DocvTransactionToken: "new-token-" + Guid.NewGuid(),
        DocvUrl: $"https://verify.socure.com/#/dv/new-token-{Guid.NewGuid()}",
        ReferenceId: "new-ref-" + Guid.NewGuid(),
        EvalId: "new-eval-" + Guid.NewGuid());

    private void StubStepupSuccess(SocureDocvSession session)
    {
        socureClient.RunDocvStepupAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    Outcome: IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: session)));
    }

    // --- IDOR prevention ---

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenChallengeNotFoundForUser()
    {
        var handler = CreateHandler();
        var command = new ResubmitChallengeCommand
        {
            ChallengeId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var precondFailed = Assert.IsType<PreconditionFailedResult<ResubmitChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, precondFailed.Reason);

        await socureClient.DidNotReceive().RunDocvStepupAssessmentAsync(
            Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<Address?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // --- Wrong state guard: only Resubmit may resubmit ---

    [Theory]
    [InlineData(DocVerificationStatus.Created)]
    [InlineData(DocVerificationStatus.Pending)]
    [InlineData(DocVerificationStatus.Verified)]
    [InlineData(DocVerificationStatus.Rejected)]
    [InlineData(DocVerificationStatus.Expired)]
    public async Task Handle_ShouldReturnConflict_WhenChallengeIsNotInResubmitState(
        DocVerificationStatus status)
    {
        var handler = CreateHandler();
        var challenge = status switch
        {
            DocVerificationStatus.Created => DocVerificationChallengeFactory.CreateChallenge(),
            DocVerificationStatus.Pending => DocVerificationChallengeFactory.CreatePendingChallenge(),
            DocVerificationStatus.Verified => DocVerificationChallengeFactory.CreateVerifiedChallenge(),
            DocVerificationStatus.Rejected => DocVerificationChallengeFactory.CreateRejectedChallenge(),
            DocVerificationStatus.Expired => CreateExpiredChallenge(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        var command = new ResubmitChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var precondFailed = Assert.IsType<PreconditionFailedResult<ResubmitChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, precondFailed.Reason);

        await socureClient.DidNotReceive().RunDocvStepupAssessmentAsync(
            Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<Address?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await challengeRepository.DidNotReceive()
            .CreateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- User must exist and have an email ---

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserMissing()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateResubmitChallenge();
        var command = new ResubmitChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var precondFailed = Assert.IsType<PreconditionFailedResult<ResubmitChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, precondFailed.Reason);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenUserHasNoEmail()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateResubmitChallenge();
        var command = new ResubmitChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = null });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var precondFailed = Assert.IsType<PreconditionFailedResult<ResubmitChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, precondFailed.Reason);
    }

    // --- Socure dependency failures propagate ---

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenSocureCallFails()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateResubmitChallenge();
        var command = new ResubmitChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "user@example.com" });
        socureClient.RunDocvStepupAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure unreachable"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<ResubmitChallengeResponse>>(result);
        await challengeRepository.DidNotReceive()
            .CreateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenSocureReturnsUnexpectedOutcome()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateResubmitChallenge();
        var command = new ResubmitChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "user@example.com" });
        // Socure says Matched (no DocV needed) — unexpected for a step-up call
        socureClient.RunDocvStepupAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    Outcome: IdProofingOutcome.Matched,
                    AllowIdRetry: true,
                    DocvSession: null)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var precondFailed = Assert.IsType<PreconditionFailedResult<ResubmitChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, precondFailed.Reason);
        await challengeRepository.DidNotReceive()
            .CreateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Happy path: a fresh challenge is created in Pending and the prior one stays Resubmit ---

    [Fact]
    public async Task Handle_ShouldCreateFreshChallenge_AndLeaveResubmitChallengeUntouched()
    {
        var handler = CreateHandler();
        var priorChallenge = DocVerificationChallengeFactory.CreateResubmitChallenge();
        var priorReferenceId = priorChallenge.SocureReferenceId;
        var priorEvalId = priorChallenge.EvalId;
        var priorPublicId = priorChallenge.PublicId;
        var session = FreshSession();

        var command = new ResubmitChallengeCommand
        {
            ChallengeId = priorChallenge.PublicId,
            UserId = priorChallenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(priorChallenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "user@example.com" });
        StubStepupSuccess(session);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.NotEqual(priorPublicId, response.ChallengeId);
        Assert.Equal(session.DocvUrl, response.DocvUrl);
        Assert.Equal(session.DocvTransactionToken, response.DocvTransactionToken);

        // A new challenge row was persisted in Pending state with the fresh session
        await challengeRepository.Received(1).CreateAsync(
            Arg.Is<DocVerificationChallenge>(c =>
                c.UserId == command.UserId
                && c.Status == DocVerificationStatus.Pending
                && c.SocureReferenceId == session.ReferenceId
                && c.EvalId == session.EvalId
                && c.DocvTransactionToken == session.DocvTransactionToken
                && c.DocvUrl == session.DocvUrl
                && c.DocvTokenIssuedAt != null
                && c.ExpiresAt != null),
            Arg.Any<CancellationToken>());

        // Prior Resubmit challenge is untouched — terminal stays terminal
        Assert.Equal(DocVerificationStatus.Resubmit, priorChallenge.Status);
        Assert.Equal(priorReferenceId, priorChallenge.SocureReferenceId);
        Assert.Equal(priorEvalId, priorChallenge.EvalId);
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDelegateToSocureWithDocvStepupWorkflow()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateResubmitChallenge();
        var session = FreshSession();
        var command = new ResubmitChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "user@example.com" });
        StubStepupSuccess(session);

        await handler.Handle(command, CancellationToken.None);

        // Resubmit must go through the dedicated step-up entry point — never through the
        // regular consumer_onboarding path, which can itself produce another RESUBMIT.
        await socureClient.Received(1).RunDocvStepupAssessmentAsync(
            command.UserId,
            "user@example.com",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<Address?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await socureClient.DidNotReceive().RunIdProofingAssessmentAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    private static DocVerificationChallenge CreateExpiredChallenge()
    {
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        challenge.TransitionTo(DocVerificationStatus.Expired);
        return challenge;
    }
}
