using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Medallion.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

public class RequestCardReplacementCommandHandlerTests
{
    private static readonly Guid TestUserGuid = Guid.CreateVersion7();

    private readonly IValidator<RequestCardReplacementCommand> _validator =
        new DataAnnotationsValidator<RequestCardReplacementCommand>(null!);
    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly IHouseholdRepository _repository =
        Substitute.For<IHouseholdRepository>();
    private readonly IIdProofingService _idProofingService =
        Substitute.For<IIdProofingService>();
    private readonly ISelfServiceEvaluator _evaluator =
        Substitute.For<ISelfServiceEvaluator>();
    private readonly ICardReplacementRequestRepository _cardReplacementRepo =
        Substitute.For<ICardReplacementRequestRepository>();
    private readonly IIdentifierHasher _identifierHasher =
        Substitute.For<IIdentifierHasher>();
    private readonly IDistributedLockProvider _distributedLockProvider =
        Substitute.For<IDistributedLockProvider>();
    private readonly NullLogger<RequestCardReplacementCommandHandler> _logger =
        NullLogger<RequestCardReplacementCommandHandler>.Instance;

    public RequestCardReplacementCommandHandlerTests()
    {
        // Default: IAL gate passes (no elevated requirement)
        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.None));

        // Default: self-service rules allow card replacement
        _evaluator.Evaluate(Arg.Any<SummerEbtCase>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });

        // Default: hasher returns a deterministic hash for any input
        _identifierHasher.Hash(Arg.Any<string?>()).Returns(callInfo =>
            callInfo.Arg<string?>() != null ? $"HASH_{callInfo.Arg<string>()}" : null);

        // Default: no recent card replacement requests (cooldown clear)
        _cardReplacementRepo.HasRecentRequestAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Default: distributed lock provider returns a no-op lock
        var mockLock = Substitute.For<IDistributedLock>();
        mockLock.AcquireAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDistributedSynchronizationHandle>());
        _distributedLockProvider.CreateLock(Arg.Any<string>()).Returns(mockLock);
    }

    private RequestCardReplacementCommandHandler CreateHandler() =>
        new(_validator, _resolver, _repository, _idProofingService, _evaluator,
            _cardReplacementRepo, _identifierHasher, _distributedLockProvider, _logger);

    private static ClaimsPrincipal CreateUser(string email, string? ialClaim = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Sub, TestUserGuid.ToString())
        };
        if (ialClaim != null)
            claims.Add(new Claim(JwtClaimTypes.Ial, ialClaim));
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static RequestCardReplacementCommand CreateValidCommand(
        ClaimsPrincipal? user = null,
        List<string>? caseIds = null) =>
        new()
        {
            User = user ?? CreateUser("user@example.com"),
            CaseIds = caseIds ?? new List<string> { "SEBT-001" }
        };

    private static HouseholdData CreateHouseholdWithCases(params SummerEbtCase[] cases) =>
        new()
        {
            SummerEbtCases = cases.ToList()
        };

    private void SetupResolverSuccess()
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));
    }

    private void SetupRepositoryReturns(HouseholdData householdData)
    {
        _repository.GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>()
        ).Returns(householdData);
    }

    // --- Validation tests ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCaseIdsIsEmpty()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string>());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    // --- Authorization tests ---

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenUserIalBelowRequired()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));
        _idProofingService.Evaluate(
                Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
                Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: false, RequiredLevel: UserIalLevel.IAL1plus));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ForbiddenResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenHouseholdIdentifierCannotBeResolved()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdIdentifier?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Handle_DoesNotCallResolver_WhenValidationFails()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string>());

        await handler.Handle(command, CancellationToken.None);

        await _resolver.DidNotReceive()
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsPreconditionFailed_WhenHouseholdDataNotFound()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        // Repository returns null by default (NSubstitute unconfigured)

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<PreconditionFailedResult>(result);
    }

    // --- Self-service rule gating tests ---

    [Fact]
    public async Task Handle_ReturnsPreconditionFailed_WhenPerCaseRulesDenyReplacement()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe"
            }
        ));
        _evaluator.Evaluate(Arg.Any<SummerEbtCase>())
            .Returns(new AllowedActions
            {
                CanRequestReplacementCard = false,
                CardReplacementDeniedMessageKey = "card_replacement.not_allowed"
            });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.NotAllowed, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_EvaluatesEachRequestedCase_NotHouseholdLevel()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001", "SEBT-002" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "A",
                ChildLastName = "B"
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-002",
                ChildFirstName = "C",
                ChildLastName = "D"
            }
        ));

        await handler.Handle(command, CancellationToken.None);

        _evaluator.Received(1).Evaluate(Arg.Is<SummerEbtCase>(c => c.SummerEBTCaseID == "SEBT-001"));
        _evaluator.Received(1).Evaluate(Arg.Is<SummerEbtCase>(c => c.SummerEBTCaseID == "SEBT-002"));
    }

    [Fact]
    public async Task Handle_DeniesWhenAnyRequestedCaseFailsPerCaseRules()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001", "SEBT-002" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "A",
                ChildLastName = "B"
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-002",
                ChildFirstName = "C",
                ChildLastName = "D"
            }
        ));
        // Second case is denied; first is allowed.
        _evaluator.Evaluate(Arg.Is<SummerEbtCase>(c => c.SummerEBTCaseID == "SEBT-002"))
            .Returns(new AllowedActions
            {
                CanRequestReplacementCard = false,
                CardReplacementDeniedMessageKey = "specific.denied.key"
            });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.NotAllowed, preconditionFailed.Reason);
    }

    // --- Cooldown tests (DB-backed) ---

    [Fact]
    public async Task Handle_ReturnsFailed_WhenCooldownActiveInPortalDb()
    {
        var command = CreateValidCommand();
        var household = CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                CardRequestedAt = null // State connector has no data
            });

        SetupResolverSuccess();
        SetupRepositoryReturns(household);

        // Portal DB says this case was requested recently
        _cardReplacementRepo.HasRecentRequestAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(command);

        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenNoCooldownInPortalDb()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                CardRequestedAt = null
            }
        ));

        // Default: _cardReplacementRepo.HasRecentRequestAsync returns false

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- Persistence tests ---

    [Fact]
    public async Task Handle_PersistsRequest_WhenSuccessful()
    {
        var command = CreateValidCommand();
        var household = CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                CardRequestedAt = null
            });

        SetupResolverSuccess();
        SetupRepositoryReturns(household);

        var result = await CreateHandler().Handle(command);

        Assert.IsType<SuccessResult>(result);
        await _cardReplacementRepo.Received(1).CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersistsRequestPerCase_WhenMultipleCases()
    {
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001", "SEBT-002" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "SEBT-001" },
            new SummerEbtCase { SummerEBTCaseID = "SEBT-002" }
        ));

        var result = await CreateHandler().Handle(command);

        Assert.IsType<SuccessResult>(result);
        await _cardReplacementRepo.Received(2).CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenUserIdMissingFromClaims()
    {
        // Create a user without the "sub" claim
        var claims = new List<Claim> { new(ClaimTypes.Email, "user@example.com") };
        var identity = new ClaimsIdentity(claims, "Test");
        var userWithoutSub = new ClaimsPrincipal(identity);

        var command = CreateValidCommand(user: userWithoutSub);
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "SEBT-001" }
        ));

        var result = await CreateHandler().Handle(command);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Handle_DoesNotPersist_WhenCooldownBlocks()
    {
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "SEBT-001" }
        ));

        _cardReplacementRepo.HasRecentRequestAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateHandler().Handle(command);

        await _cardReplacementRepo.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // --- Co-loaded case tests ---

    [Fact]
    public async Task Handle_ReturnsConflict_WhenRequestedCaseIsCoLoaded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-COLOADED" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-COLOADED",
                ChildFirstName = "John",
                ChildLastName = "Doe",
                IsCoLoaded = true
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_ReturnsConflict_WhenAnyRequestedCaseIsCoLoaded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001", "SEBT-COLOADED" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "Regular",
                ChildLastName = "Child",
                IsCoLoaded = false
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-COLOADED",
                ChildFirstName = "CoLoaded",
                ChildLastName = "Child",
                IsCoLoaded = true
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    // --- Success tests ---

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenValidCommandAndNoActiveCooldown()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001", "SEBT-002" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe"
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-002",
                ChildFirstName = "Jane",
                ChildLastName = "Doe"
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }

    // --- Cancellation token propagation ---

    [Fact]
    public async Task Handle_PassesCancellationTokenToResolver()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), token)
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe"
            }
        ));

        await handler.Handle(command, token);

        await _resolver.Received(1).ResolveAsync(Arg.Any<ClaimsPrincipal>(), token);
    }

    // --- IAL propagation tests ---

    [Fact]
    public async Task Handle_PassesUserIalLevelToRepository()
    {
        var handler = CreateHandler();
        var user = CreateUser("user@example.com", ialClaim: "1plus");
        var command = CreateValidCommand(user: user);
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe"
            }
        ));

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            UserIalLevel.IAL1plus,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesIalNone_WhenNoIalClaim()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe"
            }
        ));

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            UserIalLevel.None,
            Arg.Any<CancellationToken>());
    }

    // --- Hashing verification ---

    [Fact]
    public async Task Handle_HashesIdentifiersBeforeCooldownCheck()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "SEBT-001" }
        ));

        await handler.Handle(command, CancellationToken.None);

        // Verify hasher was called with the household identifier value and case ID
        _identifierHasher.Received().Hash(Arg.Any<string?>());
        await _cardReplacementRepo.Received(1).HasRecentRequestAsync(
            "HASH_user@example.com", "HASH_SEBT-001", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
