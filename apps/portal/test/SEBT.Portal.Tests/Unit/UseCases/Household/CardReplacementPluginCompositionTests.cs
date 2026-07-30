using System.Security.Claims;
using Medallion.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.UseCases.Household;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

/// <summary>
/// Pins the plugin-observable card-replacement contract across the composed
/// portal path: the real plugin read path serves opaque case tokens, the
/// handler forwards them through the Core port, and the REAL
/// <see cref="PluginCardReplacementBackend"/> adapter decodes them so the
/// contract service receives ONE batched request carrying the household
/// identifier and each case's raw routing triple — exactly what the state
/// connector has always seen.
/// </summary>
public class CardReplacementPluginCompositionTests
{
    private const string GuardianEmail = "user@example.com";
    private const string RawCaseId1 = "STATE-CASE-1";
    private const string RawCaseId2 = "STATE-CASE-2";
    private static readonly Guid TestUserGuid = Guid.CreateVersion7();

    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly IIdProofingService _idProofingService =
        Substitute.For<IIdProofingService>();
    private readonly ISelfServiceEvaluator _evaluator =
        Substitute.For<ISelfServiceEvaluator>();
    private readonly ICardReplacementService _cardReplacementService =
        Substitute.For<ICardReplacementService>();
    private readonly ICardReplacementRequestRepository _cardReplacementRepo =
        Substitute.For<ICardReplacementRequestRepository>();
    private readonly IIdentifierHasher _identifierHasher =
        Substitute.For<IIdentifierHasher>();
    private readonly IDistributedLockProvider _distributedLockProvider =
        Substitute.For<IDistributedLockProvider>();

    public CardReplacementPluginCompositionTests()
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(GuardianEmail));

        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.None));

        _evaluator.Evaluate(Arg.Any<SummerEbtCase>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });

        _cardReplacementService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.Success());

        _identifierHasher.Hash(Arg.Any<string?>()).Returns(callInfo =>
            callInfo.Arg<string?>() != null ? $"HASH_{callInfo.Arg<string>()}" : null);

        var mockLock = Substitute.For<IDistributedLock>();
        mockLock.AcquireAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDistributedSynchronizationHandle>());
        _distributedLockProvider.CreateLock(Arg.Any<string>()).Returns(mockLock);
    }

    private static PluginHouseholdData CreatePluginHousehold() => new()
    {
        Email = GuardianEmail,
        SummerEbtCases = new List<PluginSummerEbtCase>
        {
            new PluginSummerEbtCase
            {
                SummerEBTCaseID = RawCaseId1,
                ApplicationId = "APP-1",
                ApplicationStudentId = "STU-1",
                ChildFirstName = "Maria",
                ChildLastName = "Garcia",
                ChildDateOfBirth = new DateOnly(2015, 5, 15),
                HouseholdType = "DirectCert",
                EligibilityType = "SNAP"
            },
            new PluginSummerEbtCase
            {
                SummerEBTCaseID = RawCaseId2,
                ApplicationId = "APP-2",
                ApplicationStudentId = "STU-2",
                ChildFirstName = "Luis",
                ChildLastName = "Garcia",
                ChildDateOfBirth = new DateOnly(2017, 3, 2),
                HouseholdType = "DirectCert",
                EligibilityType = "SNAP"
            }
        }
    };

    /// <summary>
    /// Real plugin read path (repository + mapper + tokenizer) so case IDs
    /// arrive at the handler exactly as production serves them to the frontend.
    /// </summary>
    private HouseholdRepository CreateRepository()
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
            .Returns(_ => CreatePluginHousehold());
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

    /// <summary>Simulates the frontend: reads the household and round-trips the served case IDs.</summary>
    private static async Task<IReadOnlyList<string>> ReadServedCaseIdsAsync(HouseholdRepository repository)
    {
        var household = await repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email(GuardianEmail),
            new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true),
            UserIalLevel.IAL1plus);
        Assert.NotNull(household);
        return household.SummerEbtCases
            .Select(c =>
            {
                Assert.NotNull(c.SummerEBTCaseID);
                return c.SummerEBTCaseID!;
            })
            .ToList();
    }

    /// <summary>Handler wired to the REAL adapter over the mocked contract service.</summary>
    private RequestCardReplacementCommandHandler CreateHandler(HouseholdRepository repository) =>
        new(new DataAnnotationsValidator<RequestCardReplacementCommand>(null!),
            _resolver, repository, _idProofingService, _evaluator,
            new PluginCardReplacementBackend(_cardReplacementService),
            _cardReplacementRepo, _identifierHasher,
            new OpaqueTokenCooldownIdentityResolver(), _distributedLockProvider,
            NullLogger<RequestCardReplacementCommandHandler>.Instance);

    private static RequestCardReplacementCommand CreateCommand(IReadOnlyList<string> servedCaseIds) => new()
    {
        User = CreateUser(),
        CaseRefs = servedCaseIds
            .Select((id, index) => new CaseRefDto(id, $"APP-{index + 1}", $"STU-{index + 1}"))
            .ToList()
    };

    [Fact]
    public async Task RequestCardReplacement_TwoCases_ContractReceivesOneBatchedDecodedRequest()
    {
        var repository = CreateRepository();
        var servedCaseIds = await ReadServedCaseIdsAsync(repository);
        Assert.Equal(2, servedCaseIds.Count);

        var result = await CreateHandler(repository).Handle(CreateCommand(servedCaseIds));

        Assert.IsType<SuccessResult>(result);
        // The contract sees exactly what it saw before the Core port existed:
        // the RAW routing triples decoded from the served tokens, the real
        // household identifier, and Reason=Unspecified — in ONE batched call.
        await _cardReplacementService.Received(1).RequestCardReplacementAsync(
            Arg.Is<PluginCardReplacementRequest>(r =>
                r.HouseholdIdentifierValue == GuardianEmail &&
                r.Reason == PluginCardReplacementReason.Unspecified &&
                r.CaseRefs.Count == 2 &&
                r.CaseRefs[0].SummerEbtCaseId == RawCaseId1 &&
                r.CaseRefs[0].ApplicationId == "APP-1" &&
                r.CaseRefs[0].ApplicationStudentId == "STU-1" &&
                r.CaseRefs[1].SummerEbtCaseId == RawCaseId2 &&
                r.CaseRefs[1].ApplicationId == "APP-2" &&
                r.CaseRefs[1].ApplicationStudentId == "STU-2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacement_PolicyRejection_RoundTripsAsPreconditionFailed()
    {
        var repository = CreateRepository();
        var servedCaseIds = await ReadServedCaseIdsAsync(repository);
        _cardReplacementService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.PolicyRejected("INELIGIBLE", "Not allowed right now."));

        var result = await CreateHandler(repository).Handle(CreateCommand(servedCaseIds));

        // The contract's message reaches the API response unchanged; the failed
        // dispatch must not burn the cooldown.
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
        Assert.Equal("Not allowed right now.", preconditionFailed.Message);
        await _cardReplacementRepo.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacement_BackendError_RoundTripsAsDependencyFailed()
    {
        var repository = CreateRepository();
        var servedCaseIds = await ReadServedCaseIdsAsync(repository);
        _cardReplacementService
            .RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.BackendError("UPSTREAM_500", "Downstream broke."));

        var result = await CreateHandler(repository).Handle(CreateCommand(servedCaseIds));

        var dependencyFailed = Assert.IsType<DependencyFailedResult>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, dependencyFailed.Reason);
        Assert.Equal("Downstream broke.", dependencyFailed.Message);
        await _cardReplacementRepo.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacement_CooldownBlocked_NeverReachesTheContract()
    {
        var repository = CreateRepository();
        var servedCaseIds = await ReadServedCaseIdsAsync(repository);
        _cardReplacementRepo.HasRecentRequestAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler(repository).Handle(CreateCommand(servedCaseIds));

        Assert.IsType<ValidationFailedResult>(result);
        await _cardReplacementService.DidNotReceiveWithAnyArgs()
            .RequestCardReplacementAsync(default!, default);
    }

    [Fact]
    public async Task RequestCardReplacement_TokensDisagreeingOnHousehold_FailWithoutReachingTheContract()
    {
        var repository = CreateRepository();
        var servedCaseIds = await ReadServedCaseIdsAsync(repository);
        // A token served by a DIFFERENT household's read smuggled into the command.
        var foreignToken = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "OTHER-CASE",
            ["householdIdentifier"] = "other@example.com",
        });
        var command = new RequestCardReplacementCommand
        {
            User = CreateUser(),
            CaseRefs = new List<CaseRefDto>
            {
                new(servedCaseIds[0], "APP-1", "STU-1"),
                new(foreignToken, null, null)
            }
        };

        var result = await CreateHandler(repository).Handle(command);

        // The adapter fails loud on the disagreement; the handler surfaces a
        // dependency failure, no contract call is made, and no cooldown burns.
        Assert.IsType<DependencyFailedResult>(result);
        await _cardReplacementService.DidNotReceiveWithAnyArgs()
            .RequestCardReplacementAsync(default!, default);
        await _cardReplacementRepo.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
