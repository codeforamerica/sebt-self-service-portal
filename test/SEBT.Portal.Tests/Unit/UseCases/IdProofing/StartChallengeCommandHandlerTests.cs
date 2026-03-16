using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
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
    private readonly ISocureClient socureClient = Substitute.For<ISocureClient>();
    private readonly IValidator<StartChallengeCommand> validator =
        new DataAnnotationsValidator<StartChallengeCommand>(null!);
    private readonly NullLogger<StartChallengeCommandHandler> logger =
        NullLogger<StartChallengeCommandHandler>.Instance;

    private StartChallengeCommandHandler CreateHandler() =>
        new(challengeRepository, userRepository, socureClient, validator, logger);

    // --- IDOR prevention (Codex test 1) ---

    [Fact]
    public async Task Handle_ShouldReturn404_WhenChallengeNotFoundForUser()
    {
        var handler = CreateHandler();
        var command = new StartChallengeCommand
        {
            ChallengeId = Guid.NewGuid(),
            UserId = 1
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 2);
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1 // Different user
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
            .StartDocvSessionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Stored DocV data path (single-call design) ---

    [Fact]
    public async Task Handle_ShouldUseStoredDocvData_WhenChallengeAlreadyHasToken()
    {
        var handler = CreateHandler();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var expectedToken = challenge.DocvTransactionToken!;
        var expectedUrl = challenge.DocvUrl!;

        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1
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
            .StartDocvSessionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Should NOT need to look up the user
        await userRepository.DidNotReceive()
            .GetUserByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
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
            UserId = 1
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
        {
            c.DocvTransactionToken = null;
            c.DocvUrl = null;
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
        {
            c.DocvTransactionToken = null;
            c.DocvUrl = null;
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1
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
            UserId = 1
        };

        challengeRepository.GetByPublicIdAsync(Guid.Empty, 1, Arg.Any<CancellationToken>())
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(-5); // Expired 5 minutes ago
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
        {
            c.ExpiresAt = DateTime.UtcNow.AddMinutes(30); // Still valid
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1
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
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId: 1, c =>
        {
            c.ExpiresAt = null; // No expiration
        });
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = 1
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.NotNull(response.DocvTransactionToken);
    }
}
