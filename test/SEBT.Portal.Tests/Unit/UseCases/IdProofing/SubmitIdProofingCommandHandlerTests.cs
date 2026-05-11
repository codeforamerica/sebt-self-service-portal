using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Household;
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
    private readonly IHouseholdRepository householdRepository = Substitute.For<IHouseholdRepository>();
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
        new(userRepository, householdRepository, challengeRepository, socureClient, socureSettings, validator, logger);

    private static SubmitIdProofingCommand CreateValidCommand(
        Guid? userId = null,
        string dob = "1990-01-01",
        string? idType = "ssn",
        string? idValue = "999-99-9999") =>
        new()
        {
            UserId = userId ?? Guid.CreateVersion7(),
            DateOfBirth = dob,
            IdType = idType,
            IdValue = idValue
        };

    // --- Validation tests ---

    [Fact]
    public async Task Handle_ShouldReturnValidationFailed_WhenDateOfBirthIsMissing()
    {
        var handler = CreateHandler();
        var command = new SubmitIdProofingCommand { UserId = Guid.NewGuid(), DateOfBirth = "" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
    }

    // --- Null idType: co-loaded users still need a benefit ID (householding); non-co-loaded fall through to Socure DocV ---

    [Fact]
    public async Task Handle_ShouldReturnFailed_WhenIdTypeIsNullAndUserIsCoLoaded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: null, idValue: null);

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com", IsCoLoaded = true });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("failed", response.Result);
        Assert.Equal("noIdProvided", response.OffboardingReason);
        Assert.Null(response.ChallengeId);

        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallSocureWithNullIdentifier_WhenIdTypeIsNullAndUserIsNotCoLoaded()
    {
        // Socure's consumer_onboarding workflow short-circuits to DocV when KYC can't resolve
        // the consumer; national_id is not required. Non-co-loaded users who pick "none of the
        // above" should reach Socure, not the noIdProvided off-board.
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: null, idValue: null);

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com", IsCoLoaded = false });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                null, null, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.DocumentVerificationRequired, AllowIdRetry: true)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("documentVerificationRequired", response.Result);
        Assert.NotNull(response.ChallengeId);

        await socureClient.Received(1)
            .RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                null, null, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
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
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // --- Co-loaded + SNAP/TANF: streamline to IAL1+ without Socure ---

    [Fact]
    public async Task Handle_ShouldCompleteProofingWithoutSocure_WhenCoLoadedAndWarehouseIcDobMatches()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(
            dob: "1984-03-05",
            idType: "snapAccountId",
            idValue: "IC000001");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = true,
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                "IC000001",
                new DateOnly(1984, 3, 5),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("matched", result.Value.Result);
        Assert.Equal(UserIalLevel.IAL1plus, user.IalLevel);
        Assert.Equal(IdProofingStatus.Completed, user.IdProofingStatus);
        await householdRepository.Received(1).TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "IC000001",
            new DateOnly(1984, 3, 5),
            Arg.Any<CancellationToken>());
        await householdRepository.DidNotReceive()
            .GetHouseholdByEmailAsync(
                Arg.Any<string>(),
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                Arg.Any<CancellationToken>());
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellation_WhenWarehouseIcDobCallIsCancelled()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(
            dob: "1984-03-05",
            idType: "snapAccountId",
            idValue: "IC000001");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = true,
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.Handle(command, CancellationToken.None));

        await householdRepository.DidNotReceive()
            .GetHouseholdByEmailAsync(
                Arg.Any<string>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCompleteProofingWithoutSocure_WhenCoLoadedAndSnapAccountMatchesUserSnapId()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "snapAccountId", idValue: "SNAP-CO-001");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = true,
            SnapId = "SNAP-CO-001",
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.GetHouseholdByEmailAsync(
                user.Email,
                Arg.Any<PiiVisibility>(),
                user.IalLevel,
                Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("matched", result.Value.Result);
        Assert.Equal(IdProofingStatus.Completed, user.IdProofingStatus);
        Assert.Equal(UserIalLevel.IAL1plus, user.IalLevel);
        Assert.NotNull(user.IdProofingCompletedAt);
        Assert.Equal(0, user.IdProofingAttemptCount);

        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCompleteProofingWithoutSocure_WhenCoLoadedAndSnapPersonMatchesHouseholdCase()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "snapPersonId", idValue: "SNAP-PERSON-CO-001");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = true,
            IdProofingAttemptCount = 0
        };
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    IsCoLoaded = true,
                    IssuanceType = IssuanceType.SnapEbtCard,
                    ApplicationStudentId = "SNAP-PERSON-CO-001"
                }
            ]
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.GetHouseholdByEmailAsync(
                user.Email,
                Arg.Any<PiiVisibility>(),
                user.IalLevel,
                Arg.Any<CancellationToken>())
            .Returns(household);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("matched", result.Value.Result);
        Assert.Equal(UserIalLevel.IAL1plus, user.IalLevel);
        Assert.Equal(IdProofingStatus.Completed, user.IdProofingStatus);

        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailedAndIncrementAttempts_WhenCoLoadedBenefitIdDoesNotMatch()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "snapAccountId", idValue: "wrong-id");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = true,
            SnapId = "SNAP-CO-001",
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.GetHouseholdByEmailAsync(
                user.Email,
                Arg.Any<PiiVisibility>(),
                user.IalLevel,
                Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("failed", result.Value.Result);
        Assert.Equal("idProofingFailed", result.Value.OffboardingReason);
        Assert.True(result.Value.AllowIdRetry);
        Assert.Equal(1, user.IdProofingAttemptCount);
        await userRepository.Received(1).UpdateUserAsync(user, Arg.Any<CancellationToken>());

        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistDateOfBirth_WhenSubmittedDobIsParseable()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(
            dob: "1984-03-05",
            idType: "snapAccountId",
            idValue: "wrong-id");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = true,
            SnapId = "SNAP-CO-001",
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.GetHouseholdByEmailAsync(
                user.Email, Arg.Any<PiiVisibility>(), user.IalLevel, Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(new DateOnly(1984, 3, 5), user.DateOfBirth);
        await userRepository.Received(1).UpdateUserAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailed_WhenDateOfBirthIsMalformed()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(
            dob: "not-a-date",
            idType: "snapAccountId",
            idValue: "SNAP-CO-001");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var failed = Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Contains(
            failed.Errors,
            e => e.Key == nameof(SubmitIdProofingCommand.DateOfBirth));

        await userRepository.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await householdRepository.DidNotReceive()
            .TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    // --- SSN/ITIN must be exactly 9 digits after stripping non-digits (DC-296 Phase 3 backend guard) ---
    // OpenAPI Individual.national_id permits 4-digit partials; product decision is full 9 only.

    [Fact]
    public async Task HandleAsync_SsnWithFewerThan9Digits_ReturnsValidationFailedAndDoesNotCallSocure()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "ssn", idValue: "12345678");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var failed = Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Contains(
            failed.Errors,
            e => e.Key == nameof(SubmitIdProofingCommand.IdValue));

        await socureClient.DidNotReceiveWithAnyArgs()
            .RunIdProofingAssessmentAsync(
                default, default!, default!,
                default, default, default, default,
                default, default, default, default,
                default);
    }

    [Fact]
    public async Task HandleAsync_SsnWithMoreThan9Digits_ReturnsValidationFailedAndDoesNotCallSocure()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "ssn", idValue: "1234567890");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var failed = Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Contains(
            failed.Errors,
            e => e.Key == nameof(SubmitIdProofingCommand.IdValue));

        await socureClient.DidNotReceiveWithAnyArgs()
            .RunIdProofingAssessmentAsync(
                default, default!, default!,
                default, default, default, default,
                default, default, default, default,
                default);
    }

    [Fact]
    public async Task HandleAsync_SsnWithNonDigits_ReturnsValidationFailedAndDoesNotCallSocure()
    {
        var handler = CreateHandler();
        // 6 digits after stripping letters; should fail the 9-digit check
        var command = CreateValidCommand(idType: "ssn", idValue: "abc123456");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var failed = Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Contains(
            failed.Errors,
            e => e.Key == nameof(SubmitIdProofingCommand.IdValue));

        await socureClient.DidNotReceiveWithAnyArgs()
            .RunIdProofingAssessmentAsync(
                default, default!, default!,
                default, default, default, default,
                default, default, default, default,
                default);
    }

    [Fact]
    public async Task HandleAsync_ItinWithInvalidLength_ReturnsValidationFailedAndDoesNotCallSocure()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "itin", idValue: "12345");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var failed = Assert.IsType<ValidationFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Contains(
            failed.Errors,
            e => e.Key == nameof(SubmitIdProofingCommand.IdValue));

        await socureClient.DidNotReceiveWithAnyArgs()
            .RunIdProofingAssessmentAsync(
                default, default!, default!,
                default, default, default, default,
                default, default, default, default,
                default);
    }

    [Fact]
    public async Task HandleAsync_Ssn9DigitsWithHyphens_NormalizesAndProceeds()
    {
        var handler = CreateHandler();
        // Hyphens are optional per OpenAPI; after stripping we have 9 digits and the guard lets us through.
        var command = CreateValidCommand(idType: "ssn", idValue: "123-45-6789");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await socureClient.Received(1)
            .RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAttemptCoLoadedMatchAndSkipSocure_WhenSnapIdSubmittedRegardlessOfPriorIsCoLoaded()
    {
        // Co-loaded status is discovered by the match attempt itself, so the precondition
        // gate on user.IsCoLoaded was removed. SNAP/TANF id types are an in-portal lookup
        // and must never reach Socure as national_id.
        var handler = CreateHandler();
        var command = CreateValidCommand(
            dob: "1984-03-05",
            idType: "snapPersonId",
            idValue: "987654321");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = false,
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        householdRepository.GetHouseholdByEmailAsync(
                Arg.Any<string>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        await handler.Handle(command, CancellationToken.None);

        await householdRepository.Received(1).TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "987654321", new DateOnly(1984, 3, 5), Arg.Any<CancellationToken>());
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistIsCoLoadedTrue_WhenWarehouseMatchSucceeds()
    {
        // The user-level co-loaded flag is derived from the match: starts false,
        // becomes true after a successful warehouse IC+DOB match.
        var handler = CreateHandler();
        var command = CreateValidCommand(
            dob: "1984-03-05",
            idType: "snapAccountId",
            idValue: "IC000001");
        var user = new User
        {
            Id = command.UserId,
            Email = "test@example.com",
            IsCoLoaded = false,
            IdProofingAttemptCount = 0
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                "IC000001", new DateOnly(1984, 3, 5), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("matched", result.Value.Result);
        Assert.True(user.IsCoLoaded);
        Assert.NotNull(user.CoLoadedLastUpdated);
    }

    [Fact]
    public async Task Handle_ShouldCallSocure_WhenCoLoadedAndSnapIdButIdValueMissing()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(idType: "snapAccountId", idValue: null);

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com", IsCoLoaded = true });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        await socureClient.Received(1)
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("matched", response.Result);
        Assert.Null(response.ChallengeId);
        Assert.Null(response.OffboardingReason);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProofingStatus_WhenSocureReturnsMatched()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var user = new User { Id = command.UserId, Email = "test@example.com" };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(IdProofingStatus.Completed, user.IdProofingStatus);
        Assert.Equal(UserIalLevel.IAL1plus, user.IalLevel);
        Assert.NotNull(user.IdProofingCompletedAt);
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
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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

    // --- ID-proofing snapshot stored on challenge (used to re-mint stale DocV tokens) ---

    [Fact]
    public async Task Handle_ShouldStoreProofingSnapshotAndTokenIssuedAt_WhenAssessmentIncludesDocvSession()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(dob: "1985-07-14", idType: "ssn", idValue: "111-22-3333");
        var docvSession = new SocureDocvSession("token-xyz", "https://verify.socure.com/#/dv/token-xyz", "ref-x", "eval-y");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: docvSession)));

        var beforeHandle = DateTime.UtcNow;
        var result = await handler.Handle(command, CancellationToken.None);
        var afterHandle = DateTime.UtcNow;

        Assert.True(result.IsSuccess);

        await challengeRepository.Received(1)
            .CreateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.ProofingDateOfBirth == "1985-07-14"
                && c.ProofingIdType == "ssn"
                && c.ProofingIdValue == "111-22-3333"
                && c.DocvTokenIssuedAt.HasValue
                && c.DocvTokenIssuedAt.Value >= beforeHandle
                && c.DocvTokenIssuedAt.Value <= afterHandle),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStoreProofingSnapshotWithoutTokenIssuedAt_WhenAssessmentHasNoDocvSession()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(dob: "1985-07-14", idType: "ssn", idValue: "111-22-3333");

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        // DocumentVerificationRequired without a DocvSession — StartChallenge's Socure-fallback
        // path will mint the token later. The challenge still needs the proofing snapshot so that
        // a future stale-token refresh has the inputs it needs.
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocvSession: null)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        await challengeRepository.Received(1)
            .CreateAsync(Arg.Is<DocVerificationChallenge>(c =>
                c.ProofingDateOfBirth == "1985-07-14"
                && c.ProofingIdType == "ssn"
                && c.ProofingIdValue == "111-22-3333"
                && c.DocvTokenIssuedAt == null),
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
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure API returned an error"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<SubmitIdProofingResponse>>(result);
    }

    // --- Address wired through to Socure ---

    [Fact]
    public async Task Handle_ShouldPassAddressToSocure_WhenHouseholdHasAddress()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var address = new Address
        {
            StreetAddress1 = "123 Main St",
            StreetAddress2 = "Apt 4",
            City = "Washington",
            State = "DC",
            PostalCode = "20001"
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.GetHouseholdByEmailAsync(
                "test@example.com", Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(new HouseholdData
            {
                UserProfile = new UserProfile { FirstName = "Jane", LastName = "Doe" },
                AddressOnFile = address
            });
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        await socureClient.Received(1).RunIdProofingAssessmentAsync(
            command.UserId, "test@example.com", command.DateOfBirth,
            command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), address, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFetchHouseholdWithAddressIncluded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        await householdRepository.Received(1).GetHouseholdByEmailAsync(
            "test@example.com",
            Arg.Is<PiiVisibility>(p => p.IncludeAddress && p.IncludeEmail && p.IncludePhone),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldContinueWithNullData_WhenHouseholdLookupThrows()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        householdRepository.GetHouseholdByEmailAsync(
                Arg.Any<string>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB timeout"));
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("matched", result.Value.Result);
        // Socure was still called (with null name/address)
        await socureClient.Received(1).RunIdProofingAssessmentAsync(
            command.UserId, "test@example.com", command.DateOfBirth,
            command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
            null, null, null, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSaveUserOnce_WhenSocureReturnsMatched()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var user = new User { Id = command.UserId, Email = "test@example.com" };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        // Single save: attempt count + proofing status together
        await userRepository.Received(1).UpdateUserAsync(user, Arg.Any<CancellationToken>());
        Assert.Equal(1, user.IdProofingAttemptCount);
        Assert.Equal(IdProofingStatus.Completed, user.IdProofingStatus);
    }

    // --- Retry cap (3 attempts max) ---

    [Fact]
    public async Task Handle_ShouldBlockSubmission_WhenUserHasReachedMaxAttempts()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com", IdProofingAttemptCount = 3 });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("failed", response.Result);
        Assert.Equal("maxAttemptsReached", response.OffboardingReason);
        Assert.False(response.AllowIdRetry);

        // Should NOT call Socure
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnAllowIdRetryFalse_WhenAttemptReachesMax()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        // User at 2 attempts: after this submission they'll be at 3 (max)
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com", IdProofingAttemptCount = 2 });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Failed, AllowIdRetry: true)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.AllowIdRetry);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllowIdRetryTrue_WhenAttemptsRemain()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        // User at 0 attempts: after this submission they'll be at 1 (2 remaining)
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com", IdProofingAttemptCount = 0 });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Failed, AllowIdRetry: true)));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AllowIdRetry);
    }

    [Fact]
    public async Task Handle_ShouldIncrementAttemptCount_AfterSocureCall()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        var user = new User { Id = command.UserId, Email = "test@example.com", IdProofingAttemptCount = 0 };
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        await userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IdProofingAttemptCount == 1),
            Arg.Any<CancellationToken>());
    }

    // --- DI session token passed through ---

    [Fact]
    public async Task Handle_ShouldPassDiSessionTokenToSocure_WhenProvided()
    {
        var handler = CreateHandler();
        var command = new SubmitIdProofingCommand
        {
            UserId = Guid.NewGuid(),
            DateOfBirth = "1990-01-01",
            IdType = "ssn",
            IdValue = "999-99-9999",
            DiSessionToken = "real-di-token-from-frontend"
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);
        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(),
                "real-di-token-from-frontend", Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Matched, AllowIdRetry: false)));

        await handler.Handle(command, CancellationToken.None);

        await socureClient.Received(1).RunIdProofingAssessmentAsync(
            command.UserId, "test@example.com", command.DateOfBirth,
            command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(),
            "real-di-token-from-frontend", Arg.Any<CancellationToken>());
    }

    // --- Socure failure reason propagation ---

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
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.Timeout, "Socure API request timed out."));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var depFailed = Assert.IsType<DependencyFailedResult<SubmitIdProofingResponse>>(result);
        Assert.Equal(DependencyFailedReason.Timeout, depFailed.Reason);
        Assert.Equal("Socure API request timed out.", depFailed.Message);
    }

    // --- Race condition: duplicate active challenge ---

    [Fact]
    public async Task Handle_ShouldReuseExistingChallenge_WhenCreateThrowsDuplicateRecordException()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var existingChallenge = DocVerificationChallengeFactory.CreateChallengeForUser(command.UserId);

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        // First call: no active challenge (triggers Socure + create path)
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(
                (DocVerificationChallenge?)null,  // first call (top of Handle)
                existingChallenge);               // second call (after catching DuplicateRecordException)

        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.DocumentVerificationRequired, AllowIdRetry: true)));

        // CreateAsync throws because another instance inserted first (unique index violation)
        challengeRepository.CreateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateRecordException(
                $"A record with the same unique constraint already exists for user {command.UserId}."));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("documentVerificationRequired", response.Result);
        Assert.Equal(existingChallenge.PublicId, response.ChallengeId);
        Assert.Equal(existingChallenge.AllowIdRetry, response.AllowIdRetry);
    }

    [Fact]
    public async Task Handle_ShouldRethrow_WhenCreateThrowsDuplicateRecordAndReQueryReturnsNull()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = command.UserId, Email = "test@example.com" });

        // Both calls return null (shouldn't happen, but defensive)
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        socureClient.RunIdProofingAssessmentAsync(
                command.UserId, "test@example.com", command.DateOfBirth,
                command.IdType, command.IdValue, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.DocumentVerificationRequired, AllowIdRetry: true)));

        challengeRepository.CreateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateRecordException(
                $"A record with the same unique constraint already exists for user {command.UserId}."));

        await Assert.ThrowsAsync<DuplicateRecordException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}
