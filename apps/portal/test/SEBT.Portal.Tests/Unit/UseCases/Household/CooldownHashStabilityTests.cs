using System.Security.Claims;
using Medallion.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

/// <summary>
/// Pins the cooldown hashing contract across the full read/write round trip:
/// household data is served by the real plugin read path (repository + mapper),
/// case IDs round-trip through the client exactly as served, and the string
/// reaching <see cref="IIdentifierHasher.Hash"/> for a case is always the RAW
/// state case ID. Cooldown rows are keyed by these hashes, so any change to the
/// hash input silently resets every household's cooldown — these tests must
/// keep passing with the same asserted values no matter how the read path
/// encodes <c>SummerEBTCaseID</c>.
/// </summary>
public class CooldownHashStabilityTests
{
    private const string GuardianEmail = "user@example.com";
    private const string RawCaseId = "STATE-CASE-123";
    private static readonly Guid TestUserGuid = Guid.CreateVersion7();

    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly IIdProofingService _idProofingService =
        Substitute.For<IIdProofingService>();
    private readonly ISelfServiceEvaluator _evaluator =
        Substitute.For<ISelfServiceEvaluator>();
    private readonly ICardReplacementBackend _cardReplacementBackend =
        Substitute.For<ICardReplacementBackend>();
    private readonly ICardReplacementRequestRepository _cardReplacementRepo =
        Substitute.For<ICardReplacementRequestRepository>();
    private readonly IIdentifierHasher _identifierHasher =
        Substitute.For<IIdentifierHasher>();
    private readonly IDistributedLockProvider _distributedLockProvider =
        Substitute.For<IDistributedLockProvider>();

    public CooldownHashStabilityTests()
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(GuardianEmail));

        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.None));

        _evaluator.Evaluate(Arg.Any<SummerEbtCase>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });
        _evaluator.EvaluateHousehold(Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });

        _cardReplacementBackend
            .RequestCardReplacementAsync(Arg.Any<CardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(WriteResult.Success());

        // Deterministic hash so assertions can name the exact expected input.
        _identifierHasher.Hash(Arg.Any<string?>()).Returns(callInfo =>
            callInfo.Arg<string?>() != null ? $"HASH_{callInfo.Arg<string>()}" : null);

        var mockLock = Substitute.For<IDistributedLock>();
        mockLock.AcquireAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDistributedSynchronizationHandle>());
        _distributedLockProvider.CreateLock(Arg.Any<string>()).Returns(mockLock);
    }

    private static PluginHouseholdData CreatePluginHousehold(bool isCoLoaded = false) => new()
    {
        Email = GuardianEmail,
        SummerEbtCases = new List<PluginSummerEbtCase>
        {
            new PluginSummerEbtCase
            {
                SummerEBTCaseID = RawCaseId,
                ApplicationId = "APP-9",
                ApplicationStudentId = "STU-7",
                ChildFirstName = "Maria",
                ChildLastName = "Garcia",
                ChildDateOfBirth = new DateOnly(2015, 5, 15),
                HouseholdType = "DirectCert",
                EligibilityType = "SNAP",
                IsCoLoaded = isCoLoaded
            }
        }
    };

    /// <summary>
    /// Real plugin read path (repository + mapper) so case IDs arrive at the
    /// handlers exactly as production serves them to the frontend.
    /// </summary>
    private static HouseholdRepository CreateRepository(bool isCoLoaded = false)
    {
        var pluginService = Substitute.For<ISummerEbtCaseService>();
        pluginService
            .GetHouseholdByIdentifierAsync(
                Arg.Any<PluginHouseholdIdentifierType>(),
                Arg.Any<string>(),
                Arg.Any<PluginPiiVisibility>(),
                Arg.Any<PluginIdentityAssuranceLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => CreatePluginHousehold(isCoLoaded));
        return new HouseholdRepository(pluginService, NullLogger<HouseholdRepository>.Instance);
    }

    private static ClaimsPrincipal CreateUser()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, GuardianEmail),
            new(JwtClaimTypes.Ial, "1plus"),
            new("sub", TestUserGuid.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>Simulates the frontend: reads the household and round-trips the served case ID.</summary>
    private static async Task<string> ReadServedCaseIdAsync(HouseholdRepository repository)
    {
        var household = await repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email(GuardianEmail),
            new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true),
            UserIalLevel.IAL1plus);
        Assert.NotNull(household);
        var servedId = Assert.Single(household.SummerEbtCases).SummerEBTCaseID;
        Assert.NotNull(servedId);
        return servedId;
    }

    private RequestCardReplacementCommandHandler CreateCardReplacementHandler(HouseholdRepository repository) =>
        new(new DataAnnotationsValidator<RequestCardReplacementCommand>(null!),
            _resolver, repository, _idProofingService, _evaluator,
            _cardReplacementBackend, _cardReplacementRepo, _identifierHasher,
            new OpaqueTokenCooldownIdentityResolver(), _distributedLockProvider,
            NullLogger<RequestCardReplacementCommandHandler>.Instance);

    private GetHouseholdDataQueryHandler CreateGetHouseholdDataHandler(HouseholdRepository repository)
    {
        var piiVisibilityService = Substitute.For<IPiiVisibilityService>();
        piiVisibilityService.GetVisibility(Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        var featureManager = Substitute.For<IFeatureManager>();
        featureManager.IsEnabledAsync(FeatureFlags.DeferEbtCardDataLoading).Returns(false);

        return new GetHouseholdDataQueryHandler(
            _resolver,
            repository,
            Substitute.For<IUserRepository>(),
            Substitute.For<IDocVerificationChallengeRepository>(),
            piiVisibilityService,
            _idProofingService,
            _evaluator,
            _cardReplacementRepo,
            _identifierHasher,
            new OpaqueTokenCooldownIdentityResolver(),
            new CoLoadedCohortFilterSettings(),
            featureManager,
            NullLogger<GetHouseholdDataQueryHandler>.Instance);
    }

    [Fact]
    public async Task RequestCardReplacement_CooldownCheckAndPersist_HashTheRawStateCaseId()
    {
        var repository = CreateRepository();
        var servedCaseId = await ReadServedCaseIdAsync(repository);
        var command = new RequestCardReplacementCommand
        {
            User = CreateUser(),
            CaseRefs = new List<CaseRefDto> { new(servedCaseId, "APP-9", "STU-7") }
        };

        var result = await CreateCardReplacementHandler(repository).Handle(command);

        Assert.IsType<SuccessResult>(result);
        // The hash input for the case is the RAW state case ID — never a
        // read-path encoding of it. Both hash sites (cooldown check + persist)
        // must key rows identically or cooldowns silently reset.
        _identifierHasher.Received().Hash(RawCaseId);
        await _cardReplacementRepo.Received(1).HasRecentRequestAsync(
            $"HASH_{GuardianEmail}", $"HASH_{RawCaseId}", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _cardReplacementRepo.Received(1).CreateAsync(
            $"HASH_{GuardianEmail}", $"HASH_{RawCaseId}", TestUserGuid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacement_ForwardsCaseIdToConnectorExactlyAsServedByReads()
    {
        var repository = CreateRepository();
        var servedCaseId = await ReadServedCaseIdAsync(repository);
        var command = new RequestCardReplacementCommand
        {
            User = CreateUser(),
            CaseRefs = new List<CaseRefDto> { new(servedCaseId, "APP-9", "STU-7") }
        };

        await CreateCardReplacementHandler(repository).Handle(command);

        // The handler passes case IDs to the backend port exactly as the read
        // path served them; translating to backend-native identifiers is the
        // backend's job, not the handler's.
        await _cardReplacementBackend.Received(1).RequestCardReplacementAsync(
            Arg.Is<CardReplacementRequest>(r =>
                r.CaseIds.Count == 1 && r.CaseIds[0] == servedCaseId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacement_MembershipCheckMatchesIdsServedByReads()
    {
        // A co-loaded case is rejected only when the requested ID matches a
        // household case — so a Conflict here proves the ordinal membership
        // comparison holds for IDs round-tripped from the read path.
        var repository = CreateRepository(isCoLoaded: true);
        var servedCaseId = await ReadServedCaseIdAsync(repository);
        var command = new RequestCardReplacementCommand
        {
            User = CreateUser(),
            CaseRefs = new List<CaseRefDto> { new(servedCaseId, "APP-9", "STU-7") }
        };

        var result = await CreateCardReplacementHandler(repository).Handle(command);

        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    [Fact]
    public async Task GetHouseholdData_CardRequestedAtHydration_HashesTheRawStateCaseId()
    {
        var repository = CreateRepository();
        var requestedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        _cardReplacementRepo.GetMostRecentRequestDateAsync(
                $"HASH_{GuardianEmail}", $"HASH_{RawCaseId}", Arg.Any<CancellationToken>())
            .Returns(requestedAt);

        var result = await CreateGetHouseholdDataHandler(repository)
            .Handle(new GetHouseholdDataQuery { User = CreateUser() });

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        // Hydration looked up the cooldown row under the raw-ID hash…
        _identifierHasher.Received().Hash(RawCaseId);
        await _cardReplacementRepo.Received(1).GetMostRecentRequestDateAsync(
            $"HASH_{GuardianEmail}", $"HASH_{RawCaseId}", Arg.Any<CancellationToken>());
        // …and the timestamp landed on the case.
        Assert.Equal(requestedAt, Assert.Single(success.Value.SummerEbtCases).CardRequestedAt);
    }
}
