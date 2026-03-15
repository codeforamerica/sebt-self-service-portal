using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.TestUtilities.Helpers;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class SubmitIdProofingCommandHandlerTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly ISocureClient socureClient = Substitute.For<ISocureClient>();
    private readonly SocureSettings socureSettings =
        new() { ChallengeExpirationMinutes = 30 };
    private readonly IValidator<SubmitIdProofingCommand> validator =
        new DataAnnotationsValidator<SubmitIdProofingCommand>(null!);
    private readonly NullLogger<SubmitIdProofingCommandHandler> logger =
        NullLogger<SubmitIdProofingCommandHandler>.Instance;

    private SubmitIdProofingCommandHandler CreateHandler() =>
        new(userRepository, challengeRepository, socureClient, socureSettings, validator, logger);

    private static SubmitIdProofingCommand CreateValidCommand(
        int userId = 1,
        string dob = "1990-01-01",
        string? idType = "ssn",
        string? idValue = "999-99-9999") =>
        new()
        {
            UserId = userId,
            DateOfBirth = dob,
            IdType = idType,
            IdValue = idValue
        };

    // --- Validation tests ---

    [Fact]
    public async Task Handle_ShouldReturnValidationFailed_WhenDateOfBirthIsMissing()
    {
        var handler = CreateHandler();
        var command = new SubmitIdProofingCommand { UserId = 1, DateOfBirth = "" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailed_WhenUserIdIsZero()
    {
        var handler = CreateHandler();
        var command = new SubmitIdProofingCommand { UserId = 0, DateOfBirth = "1990-01-01" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
    }

    // --- Null idType → noIdProvided (Codex test 5) ---

    [Fact]
    public async Task Handle_ShouldReturnFailed_WhenIdTypeIsNull()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: null, idValue: null);

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("failed", response.Result);
        Assert.Equal("noIdProvided", response.OffboardingReason);
        Assert.Null(response.ChallengeId);
    }

    // --- User not found ---

    [Fact]
    public async Task Handle_ShouldReturnPreconditionFailed_WhenUserNotFound()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<PreconditionFailedResult<SubmitIdProofingResponse>>(result);
    }

    // --- Active challenge reuse (D10) ---

    [Fact]
    public async Task Handle_ShouldReuseExistingActiveChallenge_WhenOneExists()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var existingChallenge = DocVerificationChallengeFactory.CreateChallengeForUser(command.UserId);

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(existingChallenge);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("documentVerificationRequired", response.Result);
        Assert.Equal(existingChallenge.PublicId, response.ChallengeId);

        // Should NOT create a new challenge
        await challengeRepository.DidNotReceive()
            .CreateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
        // Should NOT call Socure
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // --- Socure assessment: Matched ---

    [Fact]
    public async Task Handle_ShouldReturnMatched_WhenSocureReturnsMatched()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("matched", response.Result);
        Assert.Null(response.ChallengeId);
        Assert.Null(response.OffboardingReason);
    }

    // --- Socure assessment: Failed ---

    [Fact]
    public async Task Handle_ShouldReturnFailed_WhenSocureReturnsFailed()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Failed, AllowIdRetry: true)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("failed", response.Result);
        Assert.Equal("idProofingFailed", response.OffboardingReason);
        Assert.True(response.AllowIdRetry);
    }

    // --- Socure assessment: DocumentVerificationRequired ---

    [Fact]
    public async Task Handle_ShouldCreateChallenge_WhenDocVerificationRequired()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.DocumentVerificationRequired, AllowIdRetry: true)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("documentVerificationRequired", response.Result);
        Assert.NotNull(response.ChallengeId);
        Assert.True(response.AllowIdRetry);

        // Should persist the challenge
        await challengeRepository.Received(1)
            .CreateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.UserId == command.UserId && c.AllowIdRetry), Arg.Any<CancellationToken>());
    }

    // --- DocV session stored on challenge ---

    [Fact]
    public async Task Handle_ShouldStoreDocvSessionOnChallenge_WhenAssessmentIncludesDocvSession()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var docvSession = new SocureDocvSession("token-abc", "https://verify.socure.com/#/dv/token-abc", "ref-456", "eval-789");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: docvSession)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Verify the challenge was created with DocV data from the assessment
        await challengeRepository.Received(1)
            .CreateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.DocvTransactionToken == "token-abc"
                && c.DocvUrl == "https://verify.socure.com/#/dv/token-abc"
                && c.SocureReferenceId == "ref-456"
                && c.EvalId == "eval-789"),
                Arg.Any<CancellationToken>());
    }

    // --- Socure client failure ---

    [Fact]
    public async Task Handle_ShouldReturnDependencyFailed_WhenSocureClientFails()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure API returned an error"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<SubmitIdProofingResponse>>(result);
    }

    // --- Socure failure reason propagation (F6) ---

    [Fact]
    public async Task Handle_ShouldPropagateTimeoutReason_WhenSocureClientTimesOut()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.Timeout, "Socure API request timed out."));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var depFailed = Assert.IsType<DependencyFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Equal(DependencyFailedReason.Timeout, depFailed.Reason);
        Assert.Equal("Socure API request timed out.", depFailed.Message);
    }
}
