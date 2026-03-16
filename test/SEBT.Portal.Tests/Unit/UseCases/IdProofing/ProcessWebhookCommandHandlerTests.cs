using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.TestUtilities.Helpers;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class ProcessWebhookCommandHandlerTests
{
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly SocureSettings socureSettings = new() { UseStub = true };
    private readonly IValidator<ProcessWebhookCommand> validator =
        new DataAnnotationsValidator<ProcessWebhookCommand>(null!);
    private readonly NullLogger<ProcessWebhookCommandHandler> logger =
        NullLogger<ProcessWebhookCommandHandler>.Instance;

    private ProcessWebhookCommandHandler CreateHandler() =>
        new(challengeRepository, userRepository, socureSettings, validator, logger);

    private static ProcessWebhookCommand CreateValidCommand(
        string eventId = "evt-123",
        string? referenceId = "ref-456",
        string? decision = "accept") =>
        new()
        {
            EventId = eventId,
            ReferenceId = referenceId,
            DocumentDecision = decision
        };

    // --- Webhook signature rejection (Codex test 2) ---

    [Fact]
    public async Task Handle_ShouldRejectWebhook_WhenSignatureInvalid_InNonStubMode()
    {
        var settings = new SocureSettings { UseStub = false, WebhookSecret = "secret" };
        var handler = new ProcessWebhookCommandHandler(
            challengeRepository, userRepository, settings, validator, logger);

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = "ref-456",
            DocumentDecision = "accept",
            WebhookSignature = null // Missing signature
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<UnauthorizedResult>(result);

        // Should NOT update any challenge
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAcceptWebhook_WhenBearerTokenMatchesSecret()
    {
        var settings = new SocureSettings { UseStub = false, WebhookSecret = "my-webhook-secret" };
        var handler = new ProcessWebhookCommandHandler(
            challengeRepository, userRepository, settings, validator, logger);
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var user = new User { Id = challenge.UserId, Email = "test@example.com" };

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(challenge.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = "ref-456",
            DocumentDecision = "accept",
            WebhookSignature = "my-webhook-secret" // Matches the configured secret
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldRejectWebhook_WhenBearerTokenDoesNotMatchSecret()
    {
        var settings = new SocureSettings { UseStub = false, WebhookSecret = "correct-secret" };
        var handler = new ProcessWebhookCommandHandler(
            challengeRepository, userRepository, settings, validator, logger);

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = "ref-456",
            DocumentDecision = "accept",
            WebhookSignature = "wrong-secret" // Does NOT match
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<UnauthorizedResult>(result);
    }

    // --- Idempotency (Codex test 3) ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenEventAlreadyProcessed()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.SocureEventId = "evt-123"; // Already processed this event
        });

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(eventId: "evt-123", referenceId: "ref-456");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Should NOT update the challenge (idempotent)
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- No terminal state downgrade (Codex test 4) ---

    [Fact]
    public async Task Handle_ShouldNotDowngrade_WhenChallengeAlreadyVerified()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateVerifiedChallenge(c =>
        {
            c.SocureEventId = "evt-old"; // Different event
        });

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(
            eventId: "evt-late-reject",
            referenceId: "ref-456",
            decision: "reject");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Should NOT update — verified is terminal
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Successful verification ---

    [Fact]
    public async Task Handle_ShouldVerifyChallenge_WhenDecisionIsAccept()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var user = new User
        {
            Id = challenge.UserId,
            Email = "test@example.com",
            IdProofingStatus = IdProofingStatus.InProgress
        };

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(challenge.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = CreateValidCommand(decision: "accept");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Challenge should be updated to Verified
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Verified
                && c.SocureEventId == "evt-123"),
                Arg.Any<CancellationToken>());

        // User should be updated to Completed + IAL2
        await userRepository.Received(1)
            .UpdateUserAsync(Arg.Is<User>(u =>
                u.IdProofingStatus == IdProofingStatus.Completed
                && u.IalLevel == UserIalLevel.IAL2),
                Arg.Any<CancellationToken>());
    }

    // --- Rejection ---

    [Fact]
    public async Task Handle_ShouldRejectChallenge_WhenDecisionIsReject()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(decision: "reject");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Rejected
                && c.OffboardingReason == "docVerificationFailed"
                && c.SocureEventId == "evt-123"),
                Arg.Any<CancellationToken>());

        // User should NOT be updated on rejection
        await userRepository.DidNotReceive()
            .UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    // --- EvalId fallback correlation (D6) ---

    [Fact]
    public async Task Handle_ShouldFindChallengeByEvalId_WhenReferenceIdNotFound()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var user = new User { Id = challenge.UserId, Email = "test@example.com" };

        challengeRepository.GetBySocureReferenceIdAsync("ref-unknown", Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        challengeRepository.GetByEvalIdAsync("eval-789", Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(challenge.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = "ref-unknown",
            EvalId = "eval-789",
            DocumentDecision = "accept"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- No challenge found ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenNoChallengeFound()
    {
        var handler = CreateHandler();

        challengeRepository.GetBySocureReferenceIdAsync("ref-unknown", Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = "ref-unknown",
            DocumentDecision = "accept"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        // Returns success to prevent Socure retries
        Assert.True(result.IsSuccess);
    }

    // --- Concurrency conflict (F1) ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenConcurrencyConflictOnUpdate()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var user = new User { Id = challenge.UserId, Email = "test@example.com" };

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(challenge.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Simulate another thread updating the row first
        challengeRepository.UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException("Row was updated by another thread"));

        var command = CreateValidCommand(decision: "accept");
        var result = await handler.Handle(command, CancellationToken.None);

        // Should return success — the other thread handled it
        Assert.True(result.IsSuccess);

        // Should NOT attempt to update the user since the challenge save failed
        await userRepository.DidNotReceive()
            .UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    // --- Stub mode allows missing signature ---

    [Fact]
    public async Task Handle_ShouldAcceptWebhook_WhenStubModeAndNoSignature()
    {
        var handler = CreateHandler(); // socureSettings.UseStub = true
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var user = new User { Id = challenge.UserId, Email = "test@example.com" };

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(challenge.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = "ref-456",
            DocumentDecision = "accept",
            WebhookSignature = null // No signature, but stub mode
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- Terminal state protection: Rejected ---

    [Fact]
    public async Task Handle_ShouldNotDowngrade_WhenChallengeAlreadyRejected()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateRejectedChallenge();
        challenge.SocureEventId = "evt-old";

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(
            eventId: "evt-late-accept",
            referenceId: "ref-456",
            decision: "accept");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Terminal state protection: Expired ---

    [Fact]
    public async Task Handle_ShouldNotDowngrade_WhenChallengeAlreadyExpired()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        challenge.TransitionTo(DocVerificationStatus.Expired);
        challenge.SocureEventId = "evt-old";

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(
            eventId: "evt-late-accept",
            referenceId: "ref-456",
            decision: "accept");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Intermediate decisions stay Pending ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenDecisionIsReview()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(decision: "review");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocVerificationStatus.Pending, challenge.Status);
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenDecisionIsResubmit()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(decision: "resubmit");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocVerificationStatus.Pending, challenge.Status);
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Unrecognized decision ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenDecisionIsUnrecognized()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(decision: "unknown");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocVerificationStatus.Pending, challenge.Status);
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Webhook for Created (not Pending) challenge ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenChallengeIsCreatedNotPending()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallenge(); // Created state

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);

        var command = CreateValidCommand(decision: "accept");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Line 99: challenge.Status != Pending blocks the transition
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- User not found after verification ---

    [Fact]
    public async Task Handle_ShouldUpdateChallengeButNotUser_WhenUserNotFoundAfterVerification()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();

        challengeRepository.GetBySocureReferenceIdAsync("ref-456", Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(challenge.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var command = CreateValidCommand(decision: "accept");
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Challenge should still be updated to Verified
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Verified),
                Arg.Any<CancellationToken>());

        // User update should NOT be called — user was deleted
        await userRepository.DidNotReceive()
            .UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    // --- Both correlation keys null ---

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBothCorrelationKeysAreNull()
    {
        var handler = CreateHandler();

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-123",
            ReferenceId = null,
            EvalId = null,
            DocumentDecision = "accept"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // No repository calls should have been made
        await challengeRepository.DidNotReceive()
            .GetBySocureReferenceIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await challengeRepository.DidNotReceive()
            .GetByEvalIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Input sanitization: control characters rejected ---

    [Theory]
    [InlineData("evt-123\nevt-fake")]
    [InlineData("evt-123\revt-fake")]
    [InlineData("evt-123\tevt-fake")]
    [InlineData("evt\0null")]
    public async Task Handle_ShouldRejectValidation_WhenEventIdContainsControlCharacters(string eventId)
    {
        var handler = CreateHandler();

        var command = new ProcessWebhookCommand
        {
            EventId = eventId,
            ReferenceId = "ref-456",
            DocumentDecision = "accept"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Theory]
    [InlineData("ref-456\nref-fake")]
    [InlineData("eval-789\r\nfake")]
    [InlineData("accept\nreject")]
    public async Task Handle_ShouldRejectValidation_WhenAnyFieldContainsControlCharacters(string injected)
    {
        var handler = CreateHandler();

        var command = new ProcessWebhookCommand
        {
            EventId = "evt-valid",
            ReferenceId = injected,
            EvalId = injected,
            DocumentDecision = injected
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }
}
