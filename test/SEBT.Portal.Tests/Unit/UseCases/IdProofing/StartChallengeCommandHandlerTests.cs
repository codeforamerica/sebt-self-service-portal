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

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class StartChallengeCommandHandlerTests
{
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IHouseholdRepository householdRepository = Substitute.For<IHouseholdRepository>();
    private readonly ISocureClient socureClient = Substitute.For<ISocureClient>();
    private readonly SocureSettings socureSettings = new()
    {
        DocvTransactionTokenTtlMinutes = 20,
        ChallengeExpirationMinutes = 30
    };
    private readonly IValidator<StartChallengeCommand> validator =
        new DataAnnotationsValidator<StartChallengeCommand>(null!);
    private readonly NullLogger<StartChallengeCommandHandler> logger =
        NullLogger<StartChallengeCommandHandler>.Instance;

    private StartChallengeCommandHandler CreateHandler() =>
        new(challengeRepository, userRepository, householdRepository, socureClient, socureSettings, validator, logger);

    // --- IDOR prevention (Codex test 1) ---

    [Fact]
    public async Task Handle_ShouldReturn404_WhenChallengeNotFoundForUser()
    {
        var handler = CreateHandler();
        var command = new StartChallengeCommand
        {
            ChallengeId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_ShouldReturn404_WhenChallengeExistsButBelongsToDifferentUser()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid());
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid() // Different user
        };

        // Repository returns null because (publicId, userId=1) doesn't match
        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
    }

    // --- State transition validation (D7) ---

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenChallengeIsInTerminalState()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateVerifiedChallenge();
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    // --- Repeated start call returns existing token (Codex test 6) ---

    [Fact]
    public async Task Handle_ShouldReturnExistingToken_WhenChallengeAlreadyPending()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal(challenge.DocvTransactionToken, response.DocvTransactionToken);
        Assert.Equal(challenge.DocvUrl, response.DocvUrl);

        // Should NOT call Socure again
        await socureClient.DidNotReceive()
            .StartDocvSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefreshDocvToken_WhenPendingAndTokenIsStale()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ProofingDateOfBirth = "1990-01-01";
            c.ProofingIdType = "ssn";
            c.ProofingIdValue = "999-99-9999";
            c.DocvTokenIssuedAt = DateTime.UtcNow.AddMinutes(-socureSettings.DocvTransactionTokenTtlMinutes - 1);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };
        var newSession = new SocureDocvSession("new-token", "https://verify.socure.com/#/dv/new-token", "ref-x", "eval-y");

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", "1990-01-01", "ssn", "999-99-9999",
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: newSession)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-token", result.Value.DocvTransactionToken);
        await challengeRepository.Received(1)
            .UpdateAsync(
                Arg.Is<DocVerificationChallenge>(c =>
                    c.DocvTransactionToken == "new-token"
                    && c.EvalId == "eval-y"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTokenStaleButProofingSnapshotMissing()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ProofingDateOfBirth = null;
            c.ProofingIdType = null;
            c.ProofingIdValue = null;
            c.DocvTokenIssuedAt = DateTime.UtcNow.AddMinutes(-socureSettings.DocvTransactionTokenTtlMinutes - 1);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefreshDocvToken_WhenCreatedAndStoredTokenIsStale()
    {
        var handler = CreateHandler();
        var userId = Guid.NewGuid();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId, c =>
        {
            c.ProofingDateOfBirth = "1990-01-01";
            c.ProofingIdType = "ssn";
            c.ProofingIdValue = "999-99-9999";
            c.DocvTokenIssuedAt = DateTime.UtcNow.AddMinutes(-socureSettings.DocvTransactionTokenTtlMinutes - 1);
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = userId
        };
        var newSession = new SocureDocvSession("fresh", "https://verify.socure.com/#/dv/fresh", "ref-a", "eval-b");

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: newSession)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fresh", result.Value.DocvTransactionToken);
        await challengeRepository.Received(1)
            .UpdateAsync(
                Arg.Is<DocVerificationChallenge>(c =>
                    c.Status == DocVerificationStatus.Pending
                    && c.DocvTransactionToken == "fresh"),
                Arg.Any<CancellationToken>());
    }

    // --- ExpiresAt extension on successful refresh ---

    [Fact]
    public async Task Handle_ShouldExtendExpiresAt_WhenRefreshSucceedsAndChallengeNearlyExpired()
    {
        var handler = CreateHandler();
        var nearExpiry = DateTime.UtcNow.AddMinutes(1);
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ProofingDateOfBirth = "1990-01-01";
            c.ProofingIdType = "ssn";
            c.ProofingIdValue = "999-99-9999";
            c.DocvTokenIssuedAt = DateTime.UtcNow.AddMinutes(-socureSettings.DocvTransactionTokenTtlMinutes - 1);
            c.ExpiresAt = nearExpiry;
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };
        var newSession = new SocureDocvSession("new-token", "https://verify.socure.com/#/dv/new-token", "ref-x", "eval-y");

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", "1990-01-01", "ssn", "999-99-9999",
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: newSession)));

        var beforeHandle = DateTime.UtcNow;
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(challenge.ExpiresAt);
        // Should be extended to at least UtcNow + ChallengeExpirationMinutes (allowing for clock drift within the test)
        var expectedFloor = beforeHandle.AddMinutes(socureSettings.ChallengeExpirationMinutes);
        Assert.True(
            challenge.ExpiresAt.Value >= expectedFloor.AddSeconds(-1),
            $"ExpiresAt {challenge.ExpiresAt:o} should be at or after {expectedFloor:o}");
        // And definitely further out than the pre-refresh value
        Assert.True(challenge.ExpiresAt.Value > nearExpiry);
    }

    [Fact]
    public async Task Handle_ShouldNotShortenExpiresAt_WhenRefreshSucceedsAndChallengeAlreadyHasLongerExpiry()
    {
        var handler = CreateHandler();
        var farFutureExpiry = DateTime.UtcNow.AddHours(12);
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ProofingDateOfBirth = "1990-01-01";
            c.ProofingIdType = "ssn";
            c.ProofingIdValue = "999-99-9999";
            c.DocvTokenIssuedAt = DateTime.UtcNow.AddMinutes(-socureSettings.DocvTransactionTokenTtlMinutes - 1);
            c.ExpiresAt = farFutureExpiry;
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };
        var newSession = new SocureDocvSession("new-token", "https://verify.socure.com/#/dv/new-token", "ref-x", "eval-y");

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: newSession)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(farFutureExpiry, challenge.ExpiresAt);
    }

    [Fact]
    public async Task Handle_ShouldNotExtendExpiresAt_WhenRefreshFails()
    {
        var handler = CreateHandler();
        var nearExpiry = DateTime.UtcNow.AddMinutes(1);
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge(c =>
        {
            c.ProofingDateOfBirth = "1990-01-01";
            c.ProofingIdType = "ssn";
            c.ProofingIdValue = "999-99-9999";
            c.DocvTokenIssuedAt = DateTime.UtcNow.AddMinutes(-socureSettings.DocvTransactionTokenTtlMinutes - 1);
            c.ExpiresAt = nearExpiry;
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = challenge.UserId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure unavailable"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(nearExpiry, challenge.ExpiresAt);
    }

    // --- Stored DocV data path (single-call design) ---

    [Fact]
    public async Task Handle_ShouldUseStoredDocvData_WhenChallengeAlreadyHasToken()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var expectedToken = challenge.DocvTransactionToken!;
        var expectedUrl = challenge.DocvUrl!;

        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal(expectedToken, response.DocvTransactionToken);
        Assert.Equal(expectedUrl, response.DocvUrl);

        // Should NOT call Socure — data was already stored
        await socureClient.DidNotReceive()
            .StartDocvSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Should NOT need to look up the user
        await userRepository.DidNotReceive()
            .GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        // Should update challenge to Pending
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Pending),
                Arg.Any<CancellationToken>());
    }

    // --- Fallback: Socure call when no stored data ---

    [Fact]
    public async Task Handle_ShouldCallSocure_WhenChallengeHasNoStoredDocvData()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.DocvTransactionToken = null;
            c.DocvUrl = null;
            c.SocureReferenceId = null;
            c.EvalId = null;
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };
        var expectedToken = Guid.NewGuid().ToString();
        var expectedUrl = $"https://verify.socure.com/#/dv/{expectedToken}";

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.StartDocvSessionAsync(command.UserId, "test@example.com", Arg.Any<CancellationToken>())
            .Returns(Result<SocureDocvSession>.Success(
                new SocureDocvSession(expectedToken, expectedUrl, "ref-123", "eval-456")));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal(expectedToken, response.DocvTransactionToken);
        Assert.Equal(expectedUrl, response.DocvUrl);

        // Should update the challenge with Socure fields
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.SocureReferenceId == "ref-123"
                && c.EvalId == "eval-456"
                && c.Status == DocVerificationStatus.Pending),
                Arg.Any<CancellationToken>());
    }

    // --- Socure failure (fallback path) ---

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenSocureSessionFails()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.DocvTransactionToken = null;
            c.DocvUrl = null;
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.StartDocvSessionAsync(command.UserId, "test@example.com", Arg.Any<CancellationToken>())
            .Returns(Result<SocureDocvSession>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure unavailable"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<StartChallengeResponse>>(result);

        // Should NOT update challenge state on failure
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- NotSupportedException from real client (token expiration dead end) ---

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenSocureClientDoesNotSupportOnDemandSessions()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.DocvTransactionToken = null;
            c.DocvUrl = null;
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        socureClient.StartDocvSessionAsync(command.UserId, "test@example.com", Arg.Any<CancellationToken>())
            .Returns<Result<SocureDocvSession>>(_ => throw new NotSupportedException(
                "DocV tokens are generated during the evaluation call."));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<StartChallengeResponse>>(result);

        // Should NOT update challenge state
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }

    // --- Guid.Empty treated as not found ---

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenChallengeIdIsEmptyGuid()
    {
        var handler = CreateHandler();
        var command = new StartChallengeCommand
        {
            ChallengeId = Guid.Empty,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(Guid.Empty, command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, preconditionFailed.Reason);
    }

    // --- Expiration check on start ---

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCreatedChallengeHasExpired()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(-5); // Expired 5 minutes ago
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
        Assert.Contains("expired", preconditionFailed.Message, StringComparison.OrdinalIgnoreCase);

        // Should transition to Expired and persist
        await challengeRepository.Received(1)
            .UpdateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.Status == DocVerificationStatus.Expired),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCreatedChallengeHasNotExpired()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30); // Still valid
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.NotNull(response.DocvTransactionToken);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCreatedChallengeHasNullExpiresAt()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: Guid.NewGuid(), c =>
        {
            c.ExpiresAt = null; // No expiration
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = Guid.NewGuid()
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.NotNull(response.DocvTransactionToken);
    }
}
