using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
    private readonly IValidator<RequestCardReplacementCommand> _validator =
        new DataAnnotationsValidator<RequestCardReplacementCommand>(null!);
    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly IHouseholdRepository _repository =
        Substitute.For<IHouseholdRepository>();
    private readonly IMinimumIalService _minimumIalService =
        Substitute.For<IMinimumIalService>();
    private readonly ISelfServiceEvaluator _evaluator =
        Substitute.For<ISelfServiceEvaluator>();
    private readonly NullLogger<RequestCardReplacementCommandHandler> _logger =
        NullLogger<RequestCardReplacementCommandHandler>.Instance;

    public RequestCardReplacementCommandHandlerTests()
    {
        // Default: IAL gate passes (no elevated requirement)
        _minimumIalService.GetMinimumIal(Arg.Any<IReadOnlyList<SummerEbtCase>>()).Returns(UserIalLevel.None);

        // Default: self-service rules allow card replacement
        _evaluator.Evaluate(Arg.Any<SummerEbtCase>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });
    }

    private RequestCardReplacementCommandHandler CreateHandler(TimeProvider? timeProvider = null) =>
        new(_validator, _resolver, _repository, _minimumIalService, _evaluator, timeProvider ?? TimeProvider.System, _logger);

    private static ClaimsPrincipal CreateUser(string email, string? ialClaim = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
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
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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
                ChildLastName = "B",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-002",
                ChildFirstName = "C",
                ChildLastName = "D",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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
                ChildLastName = "B",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-002",
                ChildFirstName = "C",
                ChildLastName = "D",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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

    // --- Cooldown tests ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCaseIsWithinCooldownPeriod()
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
                CardRequestedAt = DateTime.UtcNow.AddDays(-3)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenRecentlyMailedCardIsStillWithinCooldown()
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
                CardRequestedAt = DateTime.UtcNow.AddDays(-10)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCaseIsOutsideCooldownPeriod()
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

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCardRequestedAtIsNull()
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
                CardRequestedAt = null
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCaseIdNotInHousehold()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(caseIds: new List<string> { "SEBT-UNKNOWN" });
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

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
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
                IsCoLoaded = true,
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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
                IsCoLoaded = false,
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-COLOADED",
                ChildFirstName = "CoLoaded",
                ChildLastName = "Child",
                IsCoLoaded = true,
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            },
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-002",
                ChildFirstName = "Jane",
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-20)
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
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
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
                ChildLastName = "Doe",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            UserIalLevel.None,
            Arg.Any<CancellationToken>());
    }

    // --- Cooldown boundary tests (using FakeTimeProvider) ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCardRequestedExactly13DaysAgo()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var handler = CreateHandler(fakeTime);
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe",
                CardRequestedAt = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCardRequestedExactly14DaysAgo()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var handler = CreateHandler(fakeTime);
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithCases(
            new SummerEbtCase
            {
                SummerEBTCaseID = "SEBT-001",
                ChildFirstName = "John",
                ChildLastName = "Doe",
                CardRequestedAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }
}
